using System.Security.Claims;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Coordination;

public sealed class UserCategoryQueryService : IUserCategoryQueries, IDisposable
{
    private const int DefaultPageSize = 15;
    private const int MaximumPageSize = 100;
    private const string PageContextPrefix = "offset:";

    private readonly IUserCategoryApiSource apiSource;
    private readonly IUserCategoryStore store;
    private readonly IAuthenticationService authenticationService;
    private readonly ILogger<UserCategoryQueryService> logger;
    private readonly Lock synchronizationLock = new();
    private QuerySession? session;
    private bool disposed;

    public UserCategoryQueryService(
        IUserCategoryApiSource apiSource,
        IUserCategoryStore store,
        IAuthenticationService authenticationService,
        ILogger<UserCategoryQueryService> logger)
    {
        this.apiSource = apiSource;
        this.store = store;
        this.authenticationService = authenticationService;
        this.logger = logger;
        authenticationService.UserChanged += OnUserChanged;
    }

    public async Task<CategoryDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.Favorite?.Id == id
            ? snapshot.Favorite
            : snapshot.Categories.FirstOrDefault(category => category.Id == id);
    }

    public async Task<UserCategoriesDto> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var limit = Math.Clamp(filter.Limit ?? DefaultPageSize, 1, MaximumPageSize);
        var offset = ParseOffset(filter.PageContext);
        var data = snapshot.Categories.Skip(offset).Take(limit).ToList();
        var nextOffset = offset + data.Count;
        var pageContext = nextOffset < snapshot.Categories.Count
            ? $"offset:{nextOffset}"
            : null;

        return new UserCategoriesDto(snapshot.Favorite, new PagedResult<CategoryDto>(pageContext, data));
    }

    public async Task<UserCategoriesDto> RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await authenticationService.InitializeAsync(cancellationToken);
        var currentSession = GetSession();

        QuerySession refreshSession;
        lock (synchronizationLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            currentSession.Token.ThrowIfCancellationRequested();
            refreshSession = currentSession;
            refreshSession.SynchronizationTask = SynchronizeCoreAsync(refreshSession);
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            refreshSession.Token);
        var token = linkedCancellation.Token;

        var snapshot = await refreshSession.SynchronizationTask.WaitAsync(token);
        token.ThrowIfCancellationRequested();

        return new UserCategoriesDto(
            snapshot.Favorite,
            new PagedResult<CategoryDto>(null, snapshot.Categories));
    }

    private async Task<UserCategorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await authenticationService.InitializeAsync(cancellationToken);
        var currentSession = GetSession();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            currentSession.Token);
        var token = linkedCancellation.Token;
        token.ThrowIfCancellationRequested();

        var snapshot = await store.GetSnapshotAsync(currentSession.UserId, token);
        token.ThrowIfCancellationRequested();

        var synchronizationTask = GetSynchronizationTask(currentSession);
        snapshot ??= await synchronizationTask.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        return snapshot;
    }

    private QuerySession GetSession()
    {
        lock (synchronizationLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var userId = GetUserId(authenticationService.CurrentUser);
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("User categories require an authenticated user.");

            return session ??= new QuerySession(userId);
        }
    }

    private Task<UserCategorySnapshot> GetSynchronizationTask(QuerySession currentSession)
    {
        lock (synchronizationLock)
        {
            currentSession.Token.ThrowIfCancellationRequested();
            if (currentSession.SynchronizationTask is null
                || currentSession.SynchronizationTask.IsFaulted
                || currentSession.SynchronizationTask.IsCanceled)
            {
                currentSession.SynchronizationTask = SynchronizeCoreAsync(currentSession);
                _ = ObserveSynchronizationAsync(currentSession.SynchronizationTask, currentSession.Token);
            }

            return currentSession.SynchronizationTask;
        }
    }

    private async Task<UserCategorySnapshot> SynchronizeCoreAsync(QuerySession currentSession)
    {
        var token = currentSession.Token;
        var categories = await apiSource.GetAllAsync(token);
        token.ThrowIfCancellationRequested();

        var snapshot = new UserCategorySnapshot(
            categories.Favorite,
            categories.Categories.Data,
            DateTimeOffset.UtcNow);
        await store.ReplaceAsync(currentSession.UserId, snapshot, token);
        token.ThrowIfCancellationRequested();
        return snapshot;
    }

    private async Task ObserveSynchronizationAsync(Task synchronizationTask, CancellationToken cancellationToken)
    {
        try
        {
            await synchronizationTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "User category cache synchronization failed; the existing snapshot was preserved.");
        }
    }

    private void OnUserChanged(ClaimsPrincipal user)
    {
        QuerySession? previousSession;
        lock (synchronizationLock)
        {
            if (session is null || string.Equals(session.UserId, GetUserId(user), StringComparison.Ordinal))
                return;

            previousSession = session;
            session = null;
        }

        previousSession.Dispose();
    }

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;

    private static int ParseOffset(string? pageContext) =>
        pageContext is not null
        && pageContext.StartsWith(PageContextPrefix, StringComparison.Ordinal)
        && int.TryParse(pageContext.AsSpan(PageContextPrefix.Length), out var offset)
        && offset >= 0
            ? offset
            : 0;

    public void Dispose()
    {
        QuerySession? previousSession;
        lock (synchronizationLock)
        {
            if (disposed)
                return;

            disposed = true;
            previousSession = session;
            session = null;
            authenticationService.UserChanged -= OnUserChanged;
        }

        previousSession?.Dispose();
    }

    private sealed class QuerySession : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();

        public QuerySession(string userId)
        {
            UserId = userId;
            Token = cancellation.Token;
        }

        public string UserId { get; }
        public CancellationToken Token { get; }
        public Task<UserCategorySnapshot>? SynchronizationTask { get; set; }

        public void Dispose()
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }
}

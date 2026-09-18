using System.Security.Claims;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Coordination;

public sealed class CategoryQueryService : ICategoryQueries, ICategoryStartup, IAsyncDisposable
{
    private const int DefaultPageSize = 15;
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;
    private const string PageContextPrefix = "offset:";

    private readonly ICategoryApiSource apiSource;
    private readonly ICategoryStore store;
    private readonly IAuthenticationService authenticationService;
    private readonly ILogger<CategoryQueryService> logger;
    private readonly Lock synchronizationLock = new();
    private Task? initializationTask;
    private Task? sessionSynchronizationTask;
    private string? sessionIdentity;
    private bool disposed;

    public CategoryQueryService(
        ICategoryApiSource apiSource,
        ICategoryStore store,
        IAuthenticationService authenticationService,
        ILogger<CategoryQueryService> logger)
    {
        this.apiSource = apiSource;
        this.store = store;
        this.authenticationService = authenticationService;
        this.logger = logger;
        authenticationService.UserChanged += OnUserChanged;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await GetInitializationTask().WaitAsync(cancellationToken);
        StartSessionSynchronization(CurrentIdentity);
    }

    public async Task<CategoryDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var identity = CurrentIdentity;
        var snapshot = await store.GetSnapshotAsync(cancellationToken);

        if (snapshot is not null && MatchesIdentity(snapshot, identity))
        {
            StartSessionSynchronization(identity);
            return snapshot.Categories.FirstOrDefault(category => category.Id == id);
        }

        await SynchronizeAsync(identity, cancellationToken);
        snapshot = await store.GetSnapshotAsync(cancellationToken);
        return MatchesIdentity(snapshot, identity)
            ? snapshot.Categories.FirstOrDefault(category => category.Id == id)
            : null;
    }

    public async Task<PagedResult<CategoryDto>> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await InitializeAsync(cancellationToken);

        var identity = CurrentIdentity;
        var snapshot = await store.GetSnapshotAsync(cancellationToken);
        if (snapshot is null || !MatchesIdentity(snapshot, identity))
        {
            await SynchronizeAsync(identity, cancellationToken);
            snapshot = await store.GetSnapshotAsync(cancellationToken);

            if (!MatchesIdentity(snapshot, identity))
                snapshot = null;
        }
        else
        {
            StartSessionSynchronization(identity);
        }

        var limit = Math.Clamp(
            filter.Limit ?? DefaultPageSize,
            MinimumPageSize,
            MaximumPageSize);
        var offset = ParseOffset(filter.PageContext);
        var data = snapshot?.Categories.Skip(offset).Take(limit).ToList() ?? [];
        var nextOffset = offset + data.Count;
        var pageContext = snapshot is not null && nextOffset < snapshot.Categories.Count
            ? $"offset:{nextOffset}"
            : null;

        return new PagedResult<CategoryDto>(pageContext, data);
    }

    private Task SynchronizeAsync(string? identity, CancellationToken cancellationToken)
    {
        return GetSessionSynchronizationTask(identity).WaitAsync(cancellationToken);
    }

    private Task GetInitializationTask()
    {
        lock (synchronizationLock)
            return initializationTask ??= store.InitializeAsync(CancellationToken.None);
    }

    private Task GetSessionSynchronizationTask(string? identity)
    {
        lock (synchronizationLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (sessionSynchronizationTask is null || sessionIdentity != identity)
            {
                sessionIdentity = identity;
                sessionSynchronizationTask = SynchronizeCoreAsync(identity);
                _ = ObserveSynchronizationAsync(sessionSynchronizationTask);
            }

            return sessionSynchronizationTask;
        }
    }

    private void StartSessionSynchronization(string? identity)
    {
        try
        {
            _ = GetSessionSynchronizationTask(identity);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ObserveSynchronizationAsync(Task synchronizationTask)
    {
        try
        {
            await synchronizationTask;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Category cache synchronization failed; the existing snapshot was preserved.");
        }
    }

    private async Task SynchronizeCoreAsync(string? identity)
    {
        await GetInitializationTask();
        var categories = await apiSource.GetAllAsync(CancellationToken.None);
        await store.ReplaceAsync(
            new CategorySnapshot(categories, DateTimeOffset.UtcNow, identity),
            CancellationToken.None);
    }

    private string? CurrentIdentity => GetUserId(authenticationService.CurrentUser);

    private static bool MatchesIdentity(CategorySnapshot? snapshot, string? identity) =>
        snapshot is not null && snapshot.Identity == identity;

    private void OnUserChanged(ClaimsPrincipal user)
    {
        lock (synchronizationLock)
        {
            if (disposed)
                return;

            sessionIdentity = null;
            sessionSynchronizationTask = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (synchronizationLock)
        {
            if (disposed)
                return ValueTask.CompletedTask;

            disposed = true;
            authenticationService.UserChanged -= OnUserChanged;
            return ValueTask.CompletedTask;
        }
    }

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;

    private static int ParseOffset(string? pageContext)
    {
        return pageContext is not null
            && pageContext.StartsWith(PageContextPrefix, StringComparison.Ordinal)
            && int.TryParse(pageContext.AsSpan(PageContextPrefix.Length), out var offset)
            && offset >= 0
            ? offset
            : 0;
    }

}

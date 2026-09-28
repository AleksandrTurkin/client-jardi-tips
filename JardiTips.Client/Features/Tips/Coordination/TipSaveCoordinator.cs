using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Features.Tips.Coordination;

public sealed class TipSaveCoordinator : ITipSaveCoordinator, IAsyncDisposable
{
    private readonly ITipApiSource tipApiSource;
    private readonly IUserCategoryQueries userCategoryQueries;
    private readonly ITipQueries tipQueries;
    private readonly IAuthenticationService authenticationService;
    private readonly IBrowserConnectivity connectivity;
    private readonly ILogger<TipSaveCoordinator> logger;
    private readonly Lock stateLock = new();
    private TipSaveState state = TipSaveState.Initial;
    private bool active;
    private bool disposed;

    public TipSaveCoordinator(
        ITipApiSource tipApiSource,
        IUserCategoryQueries userCategoryQueries,
        ITipQueries tipQueries,
        IAuthenticationService authenticationService,
        IBrowserConnectivity connectivity,
        ILogger<TipSaveCoordinator> logger)
    {
        this.tipApiSource = tipApiSource;
        this.userCategoryQueries = userCategoryQueries;
        this.tipQueries = tipQueries;
        this.authenticationService = authenticationService;
        this.connectivity = connectivity;
        this.logger = logger;
    }

    public event Action? StateChanged;

    public TipSaveState State
    {
        get
        {
            lock (stateLock)
                return state;
        }
    }

    public bool IsAuthenticated => authenticationService.IsAuthenticated;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (!active)
            {
                active = true;
                state = state with { IsOnline = connectivity.IsOnline };
                connectivity.ConnectivityChanged += OnConnectivityChanged;
            }
        }

        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async Task<TipSaveOutcome> SaveAsync(TipDto tip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tip);

        if (!authenticationService.IsAuthenticated)
            return TipSaveOutcome.Unauthorized;

        if (!connectivity.IsOnline)
            return TipSaveOutcome.Offline;

        if (!TryBeginSave())
            return TipSaveOutcome.Failed;

        try
        {
            var categoryId = await GetFavoritesCategoryIdAsync(cancellationToken);
            var createOutcome = await tipApiSource.CreateAsync(
                new CreateTipRequest(tip.Title, tip.Content, categoryId),
                cancellationToken);

            if (createOutcome == TipCreateOutcome.LimitReached)
                return TipSaveOutcome.LimitReached;

            await RefreshLocalCachesAsync(categoryId, cancellationToken);
            return TipSaveOutcome.Saved;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TipSaveOutcome.Cancelled;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to save tip {TipId} to favorites.", tip.Id);
            return TipSaveOutcome.Failed;
        }
        finally
        {
            UpdateState(current => current with { IsSaving = false });
        }
    }

    public Task DeactivateAsync()
    {
        lock (stateLock)
        {
            if (active)
            {
                active = false;
                connectivity.ConnectivityChanged -= OnConnectivityChanged;
            }
        }

        return Task.CompletedTask;
    }

    private async Task<Guid> GetFavoritesCategoryIdAsync(CancellationToken cancellationToken)
    {
        var categories = await userCategoryQueries.GetAsync(
            new CategoriesFilter(null, 1),
            cancellationToken);

        return categories.Favorite?.Id
            ?? throw new InvalidOperationException("The registered user's Favorites category is missing.");
    }

    private async Task RefreshLocalCachesAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        try
        {
            var refreshed = await userCategoryQueries.RefreshAsync(cancellationToken);
            var favoritesId = refreshed.Favorite?.Id ?? categoryId;
            await tipQueries.InvalidateAsync(favoritesId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Saved-tip cache refresh failed; the next load will re-synchronize.");
        }
    }

    private bool TryBeginSave()
    {
        lock (stateLock)
        {
            if (state.IsSaving)
                return false;

            state = state with { IsSaving = true };
        }

        StateChanged?.Invoke();
        return true;
    }

    private void OnConnectivityChanged(bool isOnline) =>
        UpdateState(current => current with { IsOnline = isOnline });

    private void UpdateState(Func<TipSaveState, TipSaveState> transition)
    {
        lock (stateLock)
        {
            if (disposed)
                return;

            state = transition(state);
        }

        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        lock (stateLock)
        {
            if (disposed)
                return;

            disposed = true;

            if (active)
            {
                active = false;
                connectivity.ConnectivityChanged -= OnConnectivityChanged;
            }

            StateChanged = null;
        }

        await Task.CompletedTask;
    }
}

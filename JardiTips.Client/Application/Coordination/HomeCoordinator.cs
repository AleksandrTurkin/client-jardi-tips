using System.Security.Claims;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Home.Models;

namespace JardiTips.Client.Application.Coordination;

public sealed class HomeCoordinator(
    ICategoryQueries categoryQueries,
    IUserCategoryQueries userCategoryQueries,
    ICategoryLikeService categoryLikeService,
    IAuthenticationService authenticationService,
    ILogger<HomeCoordinator> logger) : IHomeCoordinator
{
    private const int PageSize = 100;

    private readonly Lock stateLock = new();
    private CancellationTokenSource disposalTokenSource = new();
    private CancellationTokenSource? userCategoriesLoadTokenSource;
    private HomeState state = HomeState.Initial;
    private long userCategoriesLoadVersion;
    private bool active;
    private bool disposed;

    public event Action? StateChanged;

    public HomeState State
    {
        get
        {
            lock (stateLock)
                return state;
        }
    }

    public Task InitializeAsync()
    {
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (active)
                return Task.CompletedTask;

            active = true;
            authenticationService.UserChanged += OnUserChanged;
        }

        UpdateState(current => current with
        {
            IsUserCategoriesLoading = authenticationService.IsAuthenticated
        });

        var publicCategoriesTask = LoadCategoriesAsync();
        var userCategoriesTask = authenticationService.IsAuthenticated
            ? LoadUserCategoriesAsync(authenticationService.CurrentUser)
            : Task.CompletedTask;

        return Task.WhenAll(publicCategoriesTask, userCategoriesTask);
    }

    public Task ReloadCategoriesAsync() => LoadCategoriesAsync();

    public Task ReloadUserCategoriesAsync()
    {
        if (!authenticationService.IsAuthenticated)
            return Task.CompletedTask;

        return LoadUserCategoriesAsync(authenticationService.CurrentUser);
    }

    public async Task<HomeLikeResult> ToggleLikeAsync(CategoryDto category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (!authenticationService.IsAuthenticated || !TryBeginLike(category.Id))
            return HomeLikeResult.Ignored;

        var currentState = State;
        var previousCategories = currentState.Categories;
        var previousTopCategories = currentState.TopCategories;
        var optimistic = category with
        {
            IsLiked = !category.IsLiked,
            LikesCount = Math.Max(0, category.LikesCount + (category.IsLiked ? -1 : 1))
        };

        ApplyLikeState(optimistic);

        try
        {
            var updated = await categoryLikeService.ToggleAsync(category, CurrentDisposalToken);
            ApplyLikeState(updated);
            return HomeLikeResult.Success;
        }
        catch (OperationCanceledException) when (IsDisposalCancellationRequested)
        {
            return HomeLikeResult.Ignored;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to toggle like for category {CategoryId}.", category.Id);
            UpdateState(current => current with
            {
                Categories = previousCategories,
                TopCategories = previousTopCategories
            });
            return HomeLikeResult.Failed;
        }
        finally
        {
            EndLike(category.Id);
        }
    }

    public void SelectCategory(CategoryDto category)
    {
        ArgumentNullException.ThrowIfNull(category);
        UpdateState(current => current with { SelectedCategory = category });
    }

    public void CloseTips() =>
        UpdateState(current => current with { SelectedCategory = null });

    public async Task DeactivateAsync()
    {
        CancellationTokenSource? disposalCancellation;
        CancellationTokenSource? userLoadCancellation;

        lock (stateLock)
        {
            if (!active)
                return;

            active = false;
            authenticationService.UserChanged -= OnUserChanged;
            Interlocked.Increment(ref userCategoriesLoadVersion);
            userLoadCancellation = Interlocked.Exchange(ref userCategoriesLoadTokenSource, null);
            disposalCancellation = Interlocked.Exchange(ref disposalTokenSource, new CancellationTokenSource());
        }

        userLoadCancellation?.Cancel();
        userLoadCancellation?.Dispose();
        await disposalCancellation.CancelAsync();
        disposalCancellation.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource disposalCancellation;
        CancellationTokenSource? userLoadCancellation;

        lock (stateLock)
        {
            if (disposed)
                return;

            disposed = true;

            if (active)
            {
                active = false;
                authenticationService.UserChanged -= OnUserChanged;
            }

            Interlocked.Increment(ref userCategoriesLoadVersion);
            userLoadCancellation = Interlocked.Exchange(ref userCategoriesLoadTokenSource, null);
            disposalCancellation = disposalTokenSource;
            StateChanged = null;
        }

        userLoadCancellation?.Cancel();
        userLoadCancellation?.Dispose();
        await disposalCancellation.CancelAsync();
        disposalCancellation.Dispose();
    }

    private CancellationToken CurrentDisposalToken
    {
        get
        {
            lock (stateLock)
                return disposalTokenSource.Token;
        }
    }

    private bool IsDisposalCancellationRequested
    {
        get
        {
            lock (stateLock)
                return disposalTokenSource.IsCancellationRequested;
        }
    }

    private async Task LoadCategoriesAsync()
    {
        UpdateState(current => current with
        {
            IsLoading = true,
            HasLoadError = false
        });

        try
        {
            var loadedCategories = new List<CategoryDto>();
            var visitedPageContexts = new HashSet<string>(StringComparer.Ordinal);
            string? pageContext = null;
            var cancellationToken = CurrentDisposalToken;

            do
            {
                var page = await categoryQueries.GetAsync(
                    new CategoriesFilter(pageContext, PageSize),
                    cancellationToken);

                loadedCategories.AddRange(page.Data);
                pageContext = page.PageContext;
            }
            while (pageContext is not null && visitedPageContexts.Add(pageContext));

            var orderedCategories = loadedCategories
                .OrderByDescending(category => category.UpdatedAt)
                .ToList();

            UpdateState(current => current with
            {
                Categories = orderedCategories,
                TopCategories = orderedCategories.Take(4).ToList()
            });
        }
        catch (OperationCanceledException) when (IsDisposalCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load categories for the home page.");
            UpdateState(current => current with { HasLoadError = true });
        }
        finally
        {
            UpdateState(current => current with { IsLoading = false });
        }
    }

    private async Task LoadUserCategoriesAsync(ClaimsPrincipal user)
    {
        var userId = GetUserId(user);
        if (userId is null)
            return;

        var loadVersion = Interlocked.Increment(ref userCategoriesLoadVersion);
        UpdateState(current => current with
        {
            IsUserCategoriesLoading = true,
            HasUserCategoriesLoadError = false
        });

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CurrentDisposalToken);
        var previousCancellation = Interlocked.Exchange(ref userCategoriesLoadTokenSource, cancellation);
        previousCancellation?.Cancel();
        previousCancellation?.Dispose();

        try
        {
            var loadedCategories = new List<CategoryDto>();
            var visitedPageContexts = new HashSet<string>(StringComparer.Ordinal);
            CategoryDto? favorite = null;
            string? pageContext = null;

            do
            {
                var page = await userCategoryQueries.GetAsync(
                    new CategoriesFilter(pageContext, PageSize),
                    cancellation.Token);

                if (pageContext is null)
                    favorite = page.Favorite;

                loadedCategories.AddRange(page.Categories.Data);

                if (!string.IsNullOrWhiteSpace(page.Categories.PageContext)
                    && !visitedPageContexts.Add(page.Categories.PageContext))
                {
                    throw new InvalidOperationException("The user categories query returned a repeated page cursor.");
                }

                pageContext = page.Categories.PageContext;
            }
            while (!string.IsNullOrWhiteSpace(pageContext));

            cancellation.Token.ThrowIfCancellationRequested();
            if (loadVersion != Volatile.Read(ref userCategoriesLoadVersion)
                || !string.Equals(userId, GetUserId(authenticationService.CurrentUser), StringComparison.Ordinal))
            {
                return;
            }

            var collections = new List<CategoryDto>(loadedCategories.Count + (favorite is null ? 0 : 1));
            if (favorite is not null)
                collections.Add(favorite);

            collections.AddRange(loadedCategories);
            UpdateState(current => current with { UserCategories = collections });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to load user collections for the home page.");
            if (loadVersion == Volatile.Read(ref userCategoriesLoadVersion)
                && string.Equals(userId, GetUserId(authenticationService.CurrentUser), StringComparison.Ordinal))
            {
                UpdateState(current => current with { HasUserCategoriesLoadError = true });
            }
        }
        finally
        {
            if (loadVersion == Volatile.Read(ref userCategoriesLoadVersion)
                && string.Equals(userId, GetUserId(authenticationService.CurrentUser), StringComparison.Ordinal))
            {
                UpdateState(current => current with { IsUserCategoriesLoading = false });
            }

            Interlocked.CompareExchange(ref userCategoriesLoadTokenSource, null, cancellation);
            cancellation.Dispose();
        }
    }

    private void OnUserChanged(ClaimsPrincipal user)
    {
        var transitionVersion = Interlocked.Increment(ref userCategoriesLoadVersion);
        Interlocked.Exchange(ref userCategoriesLoadTokenSource, null)?.Cancel();
        _ = ApplyUserChangeAsync(user, transitionVersion);
    }

    private async Task ApplyUserChangeAsync(ClaimsPrincipal user, long transitionVersion)
    {
        if (transitionVersion != Volatile.Read(ref userCategoriesLoadVersion))
            return;

        UpdateState(current => current with
        {
            SelectedCategory = current.SelectedCategory is not null
                && current.UserCategories.Any(category => category.Id == current.SelectedCategory.Id)
                    ? null
                    : current.SelectedCategory,
            UserCategories = [],
            IsUserCategoriesLoading = GetUserId(user) is not null,
            HasUserCategoriesLoadError = false
        });

        await LoadCategoriesAsync();

        if (GetUserId(user) is null)
            return;

        await LoadUserCategoriesAsync(user);
    }

    private bool TryBeginLike(Guid categoryId)
    {
        lock (stateLock)
        {
            if (disposed)
                return false;

            var pendingLikeCategoryIds = state.PendingLikeCategoryIds.ToHashSet();
            if (!pendingLikeCategoryIds.Add(categoryId))
                return false;

            state = state with { PendingLikeCategoryIds = pendingLikeCategoryIds };
        }

        NotifyStateChanged();
        return true;
    }

    private void EndLike(Guid categoryId)
    {
        lock (stateLock)
        {
            var pendingLikeCategoryIds = state.PendingLikeCategoryIds.ToHashSet();
            if (!pendingLikeCategoryIds.Remove(categoryId))
                return;

            state = state with { PendingLikeCategoryIds = pendingLikeCategoryIds };
        }

        NotifyStateChanged();
    }

    private void ApplyLikeState(CategoryDto updated) =>
        UpdateState(current => current with
        {
            Categories = ReplaceCategory(current.Categories, updated),
            TopCategories = ReplaceCategory(current.TopCategories, updated)
        });

    private void UpdateState(Func<HomeState, HomeState> update)
    {
        lock (stateLock)
            state = update(state);

        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private static IReadOnlyList<CategoryDto> ReplaceCategory(
        IReadOnlyList<CategoryDto> source,
        CategoryDto updated) =>
        source.Select(existing => existing.Id == updated.Id ? updated : existing).ToList();

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;
}

using System.Security.Claims;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Home.Models;

namespace JardiTips.Client.Features.Home.Coordination;

public sealed class HomeCoordinator : IHomeCoordinator
{
    private readonly HomeCategoryLoader categoryLoader;
    private readonly ICategoryLikeService categoryLikeService;
    private readonly IAuthenticationService authenticationService;
    private readonly ILogger<HomeCoordinator> logger;
    private readonly object stateLock = new();
    private readonly HomeRequestLifetime requestLifetime = new();
    private HomeState state = HomeState.Initial;
    private Task userTransitionTask = Task.CompletedTask;
    private bool active;

    public HomeCoordinator(
        ICategoryQueries categoryQueries,
        IUserCategoryQueries userCategoryQueries,
        ICategoryLikeService categoryLikeService,
        IAuthenticationService authenticationService,
        ILogger<HomeCoordinator> logger)
    {
        categoryLoader = new HomeCategoryLoader(categoryQueries, userCategoryQueries);
        this.categoryLikeService = categoryLikeService;
        this.authenticationService = authenticationService;
        this.logger = logger;
    }

    public event Action? StateChanged;

    public HomeState State
    {
        get
        {
            lock (stateLock)
                return state;
        }
    }

    public async Task InitializeAsync()
    {
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(requestLifetime.IsDisposed, this);
            if (active)
                return;

            active = true;
            authenticationService.UserChanged += OnUserChanged;
        }

        var sessionToken = requestLifetime.BeginSession();
        var isAuthenticated = authenticationService.IsAuthenticated;
        var categoriesTask = LoadCategoriesAsync(sessionToken);
        var userCategoriesTask = isAuthenticated
            ? LoadUserCategoriesAsync(sessionToken)
            : CompleteUserCategoriesWithoutSessionAsync();

        await Task.WhenAll(categoriesTask, userCategoriesTask);
    }

    public Task ReloadCategoriesAsync()
    {
        var sessionToken = requestLifetime.BeginSession();
        return LoadCategoriesAsync(sessionToken);
    }

    public Task ReloadUserCategoriesAsync()
    {
        if (!authenticationService.IsAuthenticated)
            return Task.CompletedTask;

        var sessionToken = requestLifetime.BeginSession();
        return LoadUserCategoriesAsync(sessionToken);
    }

    public async Task<HomeLikeOutcome> ToggleLikeAsync(CategoryDto category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (!authenticationService.IsAuthenticated || !TryBeginLike(category.Id, out var original))
            return HomeLikeOutcome.Ignored;

        var optimistic = category with
        {
            IsLiked = !category.IsLiked,
            LikesCount = Math.Max(0, category.LikesCount + (category.IsLiked ? -1 : 1))
        };

        UpdateState(current => HomeStateTransitions.ReplaceCategory(current, optimistic));
        var sessionToken = requestLifetime.BeginSession();

        try
        {
            var updated = await categoryLikeService.ToggleAsync(category, sessionToken);
            UpdateState(current => HomeStateTransitions.ReplaceCategory(current, updated));
            return HomeLikeOutcome.Succeeded;
        }
        catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
        {
            return HomeLikeOutcome.Ignored;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to toggle like for category {CategoryId}.", category.Id);

            if (original is not null)
                UpdateState(current => HomeStateTransitions.RestoreCategory(current, original));

            return HomeLikeOutcome.Failed;
        }
        finally
        {
            UpdateState(current => HomeStateTransitions.EndLike(current, category.Id));
        }
    }

    public void SelectCategory(CategoryDto category)
    {
        ArgumentNullException.ThrowIfNull(category);
        UpdateState(current => HomeStateTransitions.SelectCategory(current, category));
    }

    public void CloseTips() =>
        UpdateState(HomeStateTransitions.CloseSelectedCategory);

    public async Task DeactivateAsync()
    {
        lock (stateLock)
        {
            if (!active)
                return;

            active = false;
            authenticationService.UserChanged -= OnUserChanged;
        }

        await requestLifetime.EndSessionAsync();
        await ObserveUserTransitionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        lock (stateLock)
        {
            if (requestLifetime.IsDisposed)
                return;

            if (active)
            {
                active = false;
                authenticationService.UserChanged -= OnUserChanged;
            }

            StateChanged = null;
        }

        await requestLifetime.DisposeAsync();
        await ObserveUserTransitionAsync();
    }

    private async Task CompleteUserCategoriesWithoutSessionAsync()
    {
        await Task.CompletedTask;
        UpdateState(current => current with { UserCategoriesStatus = HomeLoadStatus.NotStarted });
    }

    private async Task LoadCategoriesAsync(CancellationToken sessionToken)
    {
        UpdateState(HomeStateTransitions.BeginCategoriesLoad);

        try
        {
            var categories = await categoryLoader.LoadPublicCategoriesAsync(sessionToken);
            UpdateState(current => HomeStateTransitions.CompleteCategoriesLoad(current, categories));
        }
        catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load categories for the home page.");
            UpdateState(HomeStateTransitions.FailCategoriesLoad);
        }
    }

    private async Task LoadUserCategoriesAsync(CancellationToken sessionToken)
    {
        var userId = authenticationService.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            UpdateState(current => current with { UserCategoriesStatus = HomeLoadStatus.NotStarted });
            return;
        }

        var request = requestLifetime.BeginUserLoad(userId);
        UpdateState(HomeStateTransitions.BeginUserCategoriesLoad);

        try
        {
            var collections = await categoryLoader.LoadUserCollectionsAsync(request.Token);

            if (requestLifetime.IsCurrent(request)
                && IsCurrentUser(request.UserId))
            {
                UpdateState(current => HomeStateTransitions.CompleteUserCategoriesLoad(current, collections));
            }
        }
        catch (OperationCanceledException) when (request.Token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to load user collections for the home page.");

            if (requestLifetime.IsCurrent(request)
                && IsCurrentUser(request.UserId))
            {
                UpdateState(HomeStateTransitions.FailUserCategoriesLoad);
            }
        }
        finally
        {
            requestLifetime.CompleteUserLoad(request);
        }
    }

    private void OnUserChanged(ClaimsPrincipal user)
    {
        lock (stateLock)
        {
            if (!active || requestLifetime.IsDisposed)
                return;

            userTransitionTask = ApplyUserChangeAsync(user, userTransitionTask);
            _ = ObserveUserTransitionAsync();
        }
    }

    private async Task ApplyUserChangeAsync(ClaimsPrincipal user, Task previousTransition)
    {
        try
        {
            await previousTransition;
        }
        catch (OperationCanceledException)
        {
        }

        var sessionToken = requestLifetime.BeginSession();
        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        if (isAuthenticated)
            _ = requestLifetime.BeginUserLoad(authenticationService.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        UpdateState(current => HomeStateTransitions.ClearUserCategories(current, isAuthenticated));

        await LoadCategoriesAsync(sessionToken);

        if (isAuthenticated)
            await LoadUserCategoriesAsync(sessionToken);
    }

    private async Task ObserveUserTransitionAsync()
    {
        Task transitionTask;
        lock (stateLock)
            transitionTask = userTransitionTask;

        try
        {
            await transitionTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "A Home authentication transition failed unexpectedly.");
        }
    }

    private bool IsCurrentUser(string userId) =>
        string.Equals(
            authenticationService.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            userId,
            StringComparison.Ordinal);

    private bool TryBeginLike(Guid categoryId, out CategoryDto? original)
    {
        lock (stateLock)
        {
            original = null;

            if (requestLifetime.IsDisposed || state.PendingLikeCategoryIds.Contains(categoryId))
                return false;

            original = state.Categories.FirstOrDefault(category => category.Id == categoryId)
                ?? state.TopCategories.FirstOrDefault(category => category.Id == categoryId);
            state = HomeStateTransitions.BeginLike(state, categoryId);
        }

        NotifyStateChanged();
        return true;
    }

    private void UpdateState(Func<HomeState, HomeState> transition)
    {
        lock (stateLock)
        {
            if (requestLifetime.IsDisposed)
                return;

            state = transition(state);
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

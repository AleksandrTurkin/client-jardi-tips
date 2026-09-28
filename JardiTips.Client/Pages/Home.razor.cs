using System.Security.Claims;
using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Home.Models;
using JardiTips.Client.Infrastructure.Api;
using MudBlazor;

namespace JardiTips.Client.Pages;

public partial class Home : IAsyncDisposable
{
    private bool subscribed;
    private bool isCreatingCollection;

    private HomeState State => HomeCoordinator.State;

    private bool CanManageSelectedCategory => AuthenticationService.IsAuthenticated
        && State.UserCategoriesStatus == HomeLoadStatus.Loaded
        && State.SelectedCategory is not null
        && State.UserCategories.Any(category => category.Id == State.SelectedCategory.Id);

    protected override async Task OnInitializedAsync()
    {
        HomeCoordinator.StateChanged += OnHomeStateChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        subscribed = true;
        await HomeCoordinator.InitializeAsync();
    }

    private Task ReloadCategoriesAsync() => HomeCoordinator.ReloadCategoriesAsync();

    private Task ReloadUserCategoriesAsync() => HomeCoordinator.ReloadUserCategoriesAsync();

    private void SelectCategory(CategoryDto category) => HomeCoordinator.SelectCategory(category);

    private Task CloseTips()
    {
        HomeCoordinator.CloseTips();
        return Task.CompletedTask;
    }

    private Task HandleTipSavedAsync() => HomeCoordinator.RefreshUserCategoriesAsync();

    private Task HandleTipsChangedAsync() => HomeCoordinator.RefreshUserCategoriesAsync();

    private async Task CreateCollectionAsync()
    {
        if (isCreatingCollection || !AuthenticationService.IsAuthenticated || !Connectivity.IsOnline)
            return;

        var userId = AuthenticationService.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return;

        isCreatingCollection = true;
        try
        {
            var dialog = await DialogService.ShowAsync<JardiTips.Client.Features.Categories.Components.CreateCollectionDialog>(
                "Create collection", new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
            var result = await dialog.Result;
            if (result is null || result.Canceled || result.Data is not CreateCategoryRequest request
                || !Connectivity.IsOnline || !AuthenticationService.IsAuthenticated
                || userId != AuthenticationService.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value)
                return;

            await UserCategoryApiSource.CreateAsync(request.Name, request.Description, CancellationToken.None);
            if (userId != AuthenticationService.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value)
                return;

            Snackbar.Add("Collection created.", Severity.Success);
            try
            {
                await UserCategoryQueries.RefreshAsync();
                await HomeCoordinator.ReloadUserCategoriesAsync();
            }
            catch (Exception)
            {
                Snackbar.Add("Collection created, but the list could not be refreshed. Please reload the page.", Severity.Warning);
            }
        }
        catch (ApiException exception)
        {
            Snackbar.Add(exception.ProblemDetails.Detail ?? "The collection could not be created.", Severity.Error);
        }
        catch (Exception)
        {
            Snackbar.Add("The collection could not be created. Please try again.", Severity.Error);
        }
        finally
        {
            isCreatingCollection = false;
        }
    }

    private async Task EditCollectionAsync(CategoryDto category)
    {
        if (isCreatingCollection || !AuthenticationService.IsAuthenticated || !Connectivity.IsOnline)
            return;

        isCreatingCollection = true;
        try
        {
            var parameters = new DialogParameters<JardiTips.Client.Features.Categories.Components.CreateCollectionDialog>
            {
                { x => x.Category, category }
            };
            var dialog = await DialogService.ShowAsync<JardiTips.Client.Features.Categories.Components.CreateCollectionDialog>(
                "Edit collection", parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
            var result = await dialog.Result;
            if (result is null || result.Canceled || result.Data is not CreateCategoryRequest request
                || !Connectivity.IsOnline || !AuthenticationService.IsAuthenticated)
                return;

            await UserCategoryApiSource.UpdateAsync(category.Id, request.Name, request.Description, CancellationToken.None);
            Snackbar.Add("Collection updated.", Severity.Success);
            await RefreshUserCollectionsAsync();
        }
        catch (ApiException exception)
        {
            Snackbar.Add(exception.ProblemDetails.Detail ?? "The collection could not be updated.", Severity.Error);
            await RefreshUserCollectionsAsync();
        }
        catch (Exception)
        {
            Snackbar.Add("The collection could not be updated. Please try again.", Severity.Error);
        }
        finally
        {
            isCreatingCollection = false;
        }
    }

    private async Task DeleteCollectionAsync(CategoryDto category)
    {
        if (isCreatingCollection || !AuthenticationService.IsAuthenticated || !Connectivity.IsOnline)
            return;

        isCreatingCollection = true;
        try
        {
            var parameters = new DialogParameters<JardiTips.Client.Features.Categories.Components.DeleteCollectionConfirmationDialog>
            {
                { x => x.Name, category.Name },
                { x => x.TipsCount, category.TipsCount }
            };
            var dialog = await DialogService.ShowAsync<JardiTips.Client.Features.Categories.Components.DeleteCollectionConfirmationDialog>(
                "Delete collection", parameters, new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true });
            var result = await dialog.Result;
            if (result is null || result.Canceled || !Connectivity.IsOnline || !AuthenticationService.IsAuthenticated)
                return;

            await UserCategoryApiSource.DeleteAsync(category.Id, CancellationToken.None);
            if (State.SelectedCategory?.Id == category.Id)
                HomeCoordinator.CloseTips();

            Snackbar.Add("Collection deleted.", Severity.Success);
            await RefreshUserCollectionsAsync();
        }
        catch (ApiException exception)
        {
            Snackbar.Add(exception.ProblemDetails.Detail ?? "The collection could not be deleted.", Severity.Error);
            await RefreshUserCollectionsAsync();
        }
        catch (Exception)
        {
            Snackbar.Add("The collection could not be deleted. Please try again.", Severity.Error);
        }
        finally
        {
            isCreatingCollection = false;
        }
    }

    private async Task RefreshUserCollectionsAsync()
    {
        try
        {
            await UserCategoryQueries.RefreshAsync();
            await HomeCoordinator.ReloadUserCategoriesAsync();
        }
        catch (Exception)
        {
            Snackbar.Add("The collections list could not be refreshed. Please reload the page.", Severity.Warning);
        }
    }

    private async Task ToggleLikeAsync(CategoryDto category)
    {
        var outcome = await HomeCoordinator.ToggleLikeAsync(category);
        if (outcome == HomeLikeOutcome.Failed)
            Snackbar.Add("The like could not be updated. Please try again.", Severity.Error);
    }

    private void OnHomeStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnConnectivityChanged(bool isOnline) => _ = InvokeAsync(StateHasChanged);

    public async ValueTask DisposeAsync()
    {
        if (subscribed)
        {
            HomeCoordinator.StateChanged -= OnHomeStateChanged;
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
            subscribed = false;
        }

        await HomeCoordinator.DeactivateAsync();
    }
}

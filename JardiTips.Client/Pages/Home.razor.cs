using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Home.Models;
using MudBlazor;

namespace JardiTips.Client.Pages;

public partial class Home : IAsyncDisposable
{
    private bool subscribed;

    private HomeState State => HomeCoordinator.State;

    protected override async Task OnInitializedAsync()
    {
        HomeCoordinator.StateChanged += OnHomeStateChanged;
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

    private Task HandleTipSavedAsync() => HomeCoordinator.ReloadUserCategoriesAsync();

    private async Task ToggleLikeAsync(CategoryDto category)
    {
        var outcome = await HomeCoordinator.ToggleLikeAsync(category);
        if (outcome == HomeLikeOutcome.Failed)
            Snackbar.Add("The like could not be updated. Please try again.", Severity.Error);
    }

    private void OnHomeStateChanged() => _ = InvokeAsync(StateHasChanged);

    public async ValueTask DisposeAsync()
    {
        if (subscribed)
        {
            HomeCoordinator.StateChanged -= OnHomeStateChanged;
            subscribed = false;
        }

        await HomeCoordinator.DeactivateAsync();
    }
}

using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Home.Models;

namespace JardiTips.Client.Features.Home.Coordination;

public interface IHomeCoordinator : IAsyncDisposable
{
    event Action? StateChanged;

    HomeState State { get; }

    Task InitializeAsync();

    Task ReloadCategoriesAsync();

    Task ReloadUserCategoriesAsync();

    Task<HomeLikeOutcome> ToggleLikeAsync(CategoryDto category);

    void SelectCategory(CategoryDto category);

    void CloseTips();

    Task DeactivateAsync();
}

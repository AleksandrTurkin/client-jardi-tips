using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Home.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IHomeCoordinator : IAsyncDisposable
{
    event Action? StateChanged;

    HomeState State { get; }

    Task InitializeAsync();

    Task ReloadCategoriesAsync();

    Task ReloadUserCategoriesAsync();

    Task<HomeLikeResult> ToggleLikeAsync(CategoryDto category);

    void SelectCategory(CategoryDto category);

    void CloseTips();

    Task DeactivateAsync();
}

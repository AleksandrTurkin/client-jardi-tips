using System.Collections.Immutable;
using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Home.Models;

namespace JardiTips.Client.Features.Home.Coordination;

internal static class HomeStateTransitions
{
    public static HomeState BeginCategoriesLoad(HomeState state) =>
        state with { CategoriesStatus = HomeLoadStatus.Loading };

    public static HomeState CompleteCategoriesLoad(
        HomeState state,
        ImmutableArray<CategoryDto> categories) =>
        state with
        {
            Categories = categories,
            TopCategories = categories.Take(4).ToImmutableArray(),
            CategoriesStatus = HomeLoadStatus.Loaded
        };

    public static HomeState FailCategoriesLoad(HomeState state) =>
        state with { CategoriesStatus = HomeLoadStatus.Failed };

    public static HomeState BeginUserCategoriesLoad(HomeState state) =>
        state with { UserCategoriesStatus = HomeLoadStatus.Loading };

    public static HomeState CompleteUserCategoriesLoad(
        HomeState state,
        ImmutableArray<CategoryDto> userCategories) =>
        state with
        {
            UserCategories = userCategories,
            SelectedCategory = state.SelectedCategory is { } selected
                && state.UserCategories.Any(category => category.Id == selected.Id)
                    ? userCategories.FirstOrDefault(category => category.Id == selected.Id)
                    : state.SelectedCategory,
            UserCategoriesStatus = HomeLoadStatus.Loaded
        };

    public static HomeState FailUserCategoriesLoad(HomeState state) =>
        state with { UserCategoriesStatus = HomeLoadStatus.Failed };

    public static HomeState ClearUserCategories(HomeState state, bool isAuthenticated) =>
        state with
        {
            UserCategories = [],
            SelectedCategory = ClearSelectedUserCategory(state),
            UserCategoriesStatus = isAuthenticated
                ? HomeLoadStatus.Loading
                : HomeLoadStatus.NotStarted
        };

    public static HomeState SelectCategory(HomeState state, CategoryDto category) =>
        state with { SelectedCategory = category };

    public static HomeState CloseSelectedCategory(HomeState state) =>
        state with { SelectedCategory = null };

    public static HomeState BeginLike(HomeState state, Guid categoryId) =>
        state with { PendingLikeCategoryIds = state.PendingLikeCategoryIds.Add(categoryId) };

    public static HomeState EndLike(HomeState state, Guid categoryId) =>
        state with { PendingLikeCategoryIds = state.PendingLikeCategoryIds.Remove(categoryId) };

    public static HomeState ReplaceCategory(HomeState state, CategoryDto updated) =>
        state with
        {
            Categories = ReplaceCategory(state.Categories, updated),
            TopCategories = ReplaceCategory(state.TopCategories, updated)
        };

    public static HomeState RestoreCategory(HomeState state, CategoryDto original) =>
        ReplaceCategory(state, original);

    private static CategoryDto? ClearSelectedUserCategory(HomeState state) =>
        state.SelectedCategory is not null
        && state.UserCategories.Any(category => category.Id == state.SelectedCategory.Id)
            ? null
            : state.SelectedCategory;

    private static ImmutableArray<CategoryDto> ReplaceCategory(
        ImmutableArray<CategoryDto> source,
        CategoryDto updated) =>
        source.Select(existing => existing.Id == updated.Id ? updated : existing).ToImmutableArray();
}

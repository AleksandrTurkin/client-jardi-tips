using System.Collections.Immutable;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Features.Home.Models;

public sealed record HomeState
{
    public static readonly HomeState Initial = new();

    public ImmutableArray<CategoryDto> Categories { get; init; } = [];

    public ImmutableArray<CategoryDto> TopCategories { get; init; } = [];

    public ImmutableArray<CategoryDto> UserCategories { get; init; } = [];

    public ImmutableHashSet<Guid> PendingLikeCategoryIds { get; init; } = [];

    public CategoryDto? SelectedCategory { get; init; }

    public HomeLoadStatus CategoriesStatus { get; init; } = HomeLoadStatus.Loading;

    public HomeLoadStatus UserCategoriesStatus { get; init; } = HomeLoadStatus.NotStarted;
}

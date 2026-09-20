using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Features.Home.Models;

public sealed record HomeState
{
    public static readonly HomeState Initial = new();

    public IReadOnlyList<CategoryDto> Categories { get; init; } = [];

    public IReadOnlyList<CategoryDto> TopCategories { get; init; } = [];

    public IReadOnlyList<CategoryDto> UserCategories { get; init; } = [];

    public IReadOnlySet<Guid> PendingLikeCategoryIds { get; init; } = new HashSet<Guid>();

    public CategoryDto? SelectedCategory { get; init; }

    public bool IsLoading { get; init; } = true;

    public bool HasLoadError { get; init; }

    public bool IsUserCategoriesLoading { get; init; }

    public bool HasUserCategoriesLoadError { get; init; }
}

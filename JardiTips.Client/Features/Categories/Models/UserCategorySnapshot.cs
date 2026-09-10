namespace JardiTips.Client.Features.Categories.Models;

public sealed record UserCategorySnapshot(
    CategoryDto? Favorite,
    IReadOnlyList<CategoryDto> Categories,
    DateTimeOffset RefreshedAt);

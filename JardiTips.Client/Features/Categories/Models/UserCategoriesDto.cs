namespace JardiTips.Client.Features.Categories.Models;

public sealed record UserCategoriesDto(
    CategoryDto? Favorite,
    PagedResult<CategoryDto> Categories);

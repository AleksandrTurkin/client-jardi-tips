namespace JardiTips.Client.Features.Categories.Models;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Description,
    CategoryType Type,
    int TipsCount,
    string? CoverImageUrl,
    DateTime UpdatedAt);

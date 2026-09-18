namespace JardiTips.Client.Features.Categories.Models;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Description,
    CategoryType Type,
    int TipsCount,
    int LikesCount,
    bool IsLiked,
    string? CoverImageUrl,
    DateTime UpdatedAt);

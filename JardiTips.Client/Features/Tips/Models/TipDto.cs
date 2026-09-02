namespace JardiTips.Client.Features.Tips.Models;

public sealed record TipDto(
    Guid Id,
    string Title,
    string Content,
    Guid CategoryId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

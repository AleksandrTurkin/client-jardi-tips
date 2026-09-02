namespace JardiTips.Client.Features.Tips.Models;

public sealed record TipSnapshot(
    Guid CategoryId,
    IReadOnlyList<TipDto> Tips,
    string? PageContext,
    bool HasMore,
    DateTimeOffset RefreshedAt);

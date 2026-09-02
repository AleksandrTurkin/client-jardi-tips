namespace JardiTips.Client.Features.Tips.Models;

public sealed record TipCollection(
    IReadOnlyList<TipDto> Tips,
    bool HasMore);

namespace JardiTips.Client.Features.Categories.Models;

public sealed record CategorySnapshot(
    IReadOnlyList<CategoryDto> Categories,
    DateTimeOffset RefreshedAt);

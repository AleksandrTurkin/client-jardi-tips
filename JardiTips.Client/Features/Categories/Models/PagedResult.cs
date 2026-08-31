namespace JardiTips.Client.Features.Categories.Models;

public sealed record PagedResult<T>(string? PageContext, IReadOnlyList<T> Data);

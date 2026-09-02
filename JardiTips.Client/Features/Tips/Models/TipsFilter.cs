namespace JardiTips.Client.Features.Tips.Models;

public sealed record TipsFilter(Guid CategoryId, string? PageContext = null);

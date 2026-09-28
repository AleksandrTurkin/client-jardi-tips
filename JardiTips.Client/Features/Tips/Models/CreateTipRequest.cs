namespace JardiTips.Client.Features.Tips.Models;

public sealed record CreateTipRequest(
    string Title,
    string Content,
    Guid CategoryId);

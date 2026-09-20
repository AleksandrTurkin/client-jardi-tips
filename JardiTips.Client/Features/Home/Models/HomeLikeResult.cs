namespace JardiTips.Client.Features.Home.Models;

public sealed record HomeLikeResult(bool Succeeded, bool ShouldNotifyFailure)
{
    public static readonly HomeLikeResult Success = new(true, false);

    public static readonly HomeLikeResult Ignored = new(false, false);

    public static readonly HomeLikeResult Failed = new(false, true);
}

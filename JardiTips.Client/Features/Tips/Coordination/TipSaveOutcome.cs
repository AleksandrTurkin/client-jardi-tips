namespace JardiTips.Client.Features.Tips.Coordination;

public enum TipSaveOutcome
{
    Saved,
    Offline,
    Unauthorized,
    LimitReached,
    Cancelled,
    Failed
}

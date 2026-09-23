namespace JardiTips.Client.Features.Tips.Coordination;

public sealed record TipSaveState
{
    public static readonly TipSaveState Initial = new();

    public bool IsOnline { get; init; }

    public bool IsSaving { get; init; }
}

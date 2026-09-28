namespace JardiTips.Client.Features.Tips.Coordination;

public enum TipManagementStatus
{
    Succeeded,
    Unauthorized,
    Offline,
    LimitReached,
    NotFound,
    Invalid,
    Failed,
    Cancelled
}

public sealed record TipManagementResult(
    TipManagementStatus Status,
    bool Refreshed = true,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

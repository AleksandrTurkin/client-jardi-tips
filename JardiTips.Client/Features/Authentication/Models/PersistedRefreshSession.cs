namespace JardiTips.Client.Features.Authentication.Models;

public sealed record PersistedRefreshSession(
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

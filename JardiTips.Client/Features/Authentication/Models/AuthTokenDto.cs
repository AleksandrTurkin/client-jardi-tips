namespace JardiTips.Client.Features.Authentication.Models;

public sealed record AuthTokenDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    string TokenType = "Bearer");

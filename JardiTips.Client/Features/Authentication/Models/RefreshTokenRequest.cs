using System.ComponentModel.DataAnnotations;

namespace JardiTips.Client.Features.Authentication.Models;

public sealed record RefreshTokenRequest(
    [property: Required, StringLength(512)] string RefreshToken);

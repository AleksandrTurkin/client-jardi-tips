using System.ComponentModel.DataAnnotations;

namespace JardiTips.Client.Features.Authentication.Models;

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(320)] string Email,
    [property: Required, StringLength(200)] string Password);

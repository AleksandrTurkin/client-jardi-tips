using System.Security.Claims;
using JardiTips.Client.Features.Authentication.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IAuthenticationService
{
    event Action<ClaimsPrincipal>? UserChanged;

    ClaimsPrincipal CurrentUser { get; }

    bool IsAuthenticated { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<bool> LogoutAsync(CancellationToken cancellationToken = default);
}

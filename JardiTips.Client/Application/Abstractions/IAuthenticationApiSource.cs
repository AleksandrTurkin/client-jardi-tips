using JardiTips.Client.Features.Authentication.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IAuthenticationApiSource
{
    Task<AuthTokenDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthTokenDto> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
}

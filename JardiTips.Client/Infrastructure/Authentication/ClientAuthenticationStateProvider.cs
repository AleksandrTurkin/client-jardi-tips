using JardiTips.Client.Application.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace JardiTips.Client.Infrastructure.Authentication;

public sealed class ClientAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IAuthenticationService authenticationService;
    private AuthenticationState authenticationState;

    public ClientAuthenticationStateProvider(IAuthenticationService authenticationService)
    {
        this.authenticationService = authenticationService;
        authenticationState = new AuthenticationState(authenticationService.CurrentUser);
        authenticationService.UserChanged += HandleUserChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(authenticationState);

    private void HandleUserChanged(System.Security.Claims.ClaimsPrincipal user)
    {
        authenticationState = new AuthenticationState(user);
        NotifyAuthenticationStateChanged(Task.FromResult(authenticationState));
    }

    public void Dispose() => authenticationService.UserChanged -= HandleUserChanged;
}

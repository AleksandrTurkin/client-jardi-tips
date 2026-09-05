using System.Net.Http.Json;
using System.Net;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Authentication.Models;

namespace JardiTips.Client.Infrastructure.Api;

public sealed class AuthenticationApiSource(HttpClient httpClient) : IAuthenticationApiSource
{
    public async Task<AuthTokenDto> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PostForTokenAsync("auth/login", request, cancellationToken);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new LoginRejectedException(
                exception.ProblemDetails.Detail ?? "The email or password is invalid.",
                exception);
        }
    }

    public async Task<AuthTokenDto> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PostForTokenAsync("auth/refresh", request, cancellationToken);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new AuthenticationSessionRejectedException(exception);
        }
    }

    public async Task LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "auth/logout",
            request,
            cancellationToken);

        await ApiClient.EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<AuthTokenDto> PostForTokenAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            route,
            request,
            cancellationToken);

        await ApiClient.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<AuthTokenDto>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication API returned an empty token response.");
    }
}

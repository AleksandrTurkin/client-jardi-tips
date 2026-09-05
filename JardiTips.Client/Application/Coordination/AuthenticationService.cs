using System.Security.Claims;
using System.Text;
using System.Text.Json;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Authentication.Models;

namespace JardiTips.Client.Application.Coordination;

public sealed class AuthenticationService(
    IAuthenticationApiSource apiSource,
    IAuthenticationStore store,
    IBrowserDataCleaner browserDataCleaner,
    ILogger<AuthenticationService> logger) : IAuthenticationService, IAccessTokenProvider
{
    private static readonly TimeSpan AccessTokenRefreshWindow = TimeSpan.FromMinutes(1);
    private static readonly ClaimsPrincipal AnonymousUser = new(new ClaimsIdentity());

    private readonly Lock initializationLock = new();
    private readonly SemaphoreSlim sessionLock = new(1, 1);
    private Task? initializationTask;
    private PersistedRefreshSession? refreshSession;
    private string? accessToken;
    private DateTime accessTokenExpiresAt;

    public event Action<ClaimsPrincipal>? UserChanged;

    public ClaimsPrincipal CurrentUser { get; private set; } = AnonymousUser;

    public bool IsAuthenticated => CurrentUser.Identity?.IsAuthenticated == true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        GetInitializationTask().WaitAsync(cancellationToken);

    public async Task LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await InitializeAsync(cancellationToken);
        await sessionLock.WaitAsync(cancellationToken);

        try
        {
            var tokens = await apiSource.LoginAsync(request, cancellationToken);
            await ApplyTokensAsync(tokens, cancellationToken);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    public async Task<bool> LogoutAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await sessionLock.WaitAsync(cancellationToken);

        var remoteLogoutSucceeded = true;
        try
        {
            if (refreshSession is not null)
            {
                try
                {
                    await apiSource.LogoutAsync(
                        new RefreshTokenRequest(refreshSession.RefreshToken),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    remoteLogoutSucceeded = false;
                    logger.LogWarning(exception, "The refresh token could not be revoked during sign-out.");
                }
            }
        }
        finally
        {
            ClearInMemoryAuthentication();
            try
            {
                await browserDataCleaner.ClearAsync(CancellationToken.None);
            }
            finally
            {
                sessionLock.Release();
            }
        }

        return remoteLogoutSucceeded;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        if (HasUsableAccessToken())
            return accessToken;

        return await RefreshCoreAsync(forceRefresh: false, refreshSession?.RefreshToken, cancellationToken);
    }

    public async Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        var observedRefreshToken = refreshSession?.RefreshToken;
        return await RefreshCoreAsync(forceRefresh: true, observedRefreshToken, cancellationToken);
    }

    private Task GetInitializationTask()
    {
        lock (initializationLock)
            return initializationTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        refreshSession = await store.GetAsync(CancellationToken.None);
        if (refreshSession is null)
            return;

        if (refreshSession.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            await ClearPersistedAuthenticationAsync();
            return;
        }

        await RefreshCoreAsync(
            forceRefresh: false,
            refreshSession.RefreshToken,
            CancellationToken.None);
    }

    private async Task<string?> RefreshCoreAsync(
        bool forceRefresh,
        string? observedRefreshToken,
        CancellationToken cancellationToken)
    {
        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (refreshSession is null)
                return null;

            if (refreshSession.RefreshTokenExpiresAt <= DateTime.UtcNow)
            {
                await ClearPersistedAuthenticationAsync();
                return null;
            }

            if (HasUsableAccessToken()
                && (!forceRefresh || !string.Equals(
                    observedRefreshToken,
                    refreshSession.RefreshToken,
                    StringComparison.Ordinal)))
            {
                return accessToken;
            }

            try
            {
                var tokens = await apiSource.RefreshAsync(
                    new RefreshTokenRequest(refreshSession.RefreshToken),
                    cancellationToken);

                await ApplyTokensAsync(tokens, cancellationToken);
                return accessToken;
            }
            catch (AuthenticationSessionRejectedException exception)
            {
                logger.LogInformation(exception, "The persisted authentication session is no longer valid.");
                await ClearPersistedAuthenticationAsync();
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The authentication session could not be refreshed.");
                ClearAccessTokenAndUser();
                return null;
            }
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private async Task ApplyTokensAsync(
        AuthTokenDto tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.AccessToken)
            || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            throw new InvalidOperationException("The authentication API returned an invalid token response.");
        }

        var persistedSession = new PersistedRefreshSession(
            tokens.RefreshToken,
            tokens.RefreshTokenExpiresAt);

        await store.SaveAsync(persistedSession, cancellationToken);

        refreshSession = persistedSession;
        accessToken = tokens.AccessToken;
        accessTokenExpiresAt = tokens.AccessTokenExpiresAt;
        SetCurrentUser(CreateClaimsPrincipal(tokens.AccessToken));
    }

    private bool HasUsableAccessToken() =>
        !string.IsNullOrWhiteSpace(accessToken)
        && accessTokenExpiresAt > DateTime.UtcNow.Add(AccessTokenRefreshWindow);

    private async Task ClearPersistedAuthenticationAsync()
    {
        ClearInMemoryAuthentication();

        try
        {
            await store.DeleteAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The persisted authentication session could not be removed.");
        }
    }

    private void ClearInMemoryAuthentication()
    {
        refreshSession = null;
        ClearAccessTokenAndUser();
    }

    private void ClearAccessTokenAndUser()
    {
        accessToken = null;
        accessTokenExpiresAt = default;
        SetCurrentUser(AnonymousUser);
    }

    private void SetCurrentUser(ClaimsPrincipal user)
    {
        CurrentUser = user;
        UserChanged?.Invoke(user);
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(string token)
    {
        var segments = token.Split('.');
        if (segments.Length != 3)
            throw new InvalidOperationException("The authentication API returned an invalid access token.");

        using var payload = JsonDocument.Parse(DecodeBase64Url(segments[1]));
        var claims = new List<Claim>();

        AddClaim(payload.RootElement, "sub", ClaimTypes.NameIdentifier, claims);
        AddClaim(payload.RootElement, "email", ClaimTypes.Email, claims);
        AddClaim(payload.RootElement, "name", ClaimTypes.Name, claims);

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "Bearer",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));
    }

    private static void AddClaim(
        JsonElement payload,
        string propertyName,
        string claimType,
        ICollection<Claim> claims)
    {
        if (payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            claims.Add(new Claim(claimType, value.GetString()!));
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var paddingLength = normalized.Length % 4;
        normalized = paddingLength switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized
        };

        return Convert.FromBase64String(normalized);
    }
}

using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Authentication.Models;

namespace JardiTips.Client.Infrastructure.IndexedDb;

public sealed class IndexedDbAuthenticationStore(BrowserDatabase database) : IAuthenticationStore
{
    private const string StoreName = "authenticationSessions";
    private const string SessionKey = "current";

    public Task<PersistedRefreshSession?> GetAsync(CancellationToken cancellationToken) =>
        database.GetAsync<PersistedRefreshSession>(StoreName, SessionKey, cancellationToken);

    public Task SaveAsync(PersistedRefreshSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        return database.PutAsync(StoreName, SessionKey, session, cancellationToken);
    }

    public Task DeleteAsync(CancellationToken cancellationToken) =>
        database.DeleteAsync(StoreName, SessionKey, cancellationToken);
}

using JardiTips.Client.Features.Authentication.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IAuthenticationStore
{
    Task<PersistedRefreshSession?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(PersistedRefreshSession session, CancellationToken cancellationToken);

    Task DeleteAsync(CancellationToken cancellationToken);
}

using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ITipStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<TipSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken);

    Task ReplaceAsync(TipSnapshot snapshot, CancellationToken cancellationToken);
}

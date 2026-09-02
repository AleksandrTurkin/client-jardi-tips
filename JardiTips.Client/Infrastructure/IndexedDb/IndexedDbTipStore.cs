using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Infrastructure.IndexedDb;

public sealed class IndexedDbTipStore(BrowserDatabase database) : ITipStore
{
    private const string StoreName = "tipSnapshots";
    private const string SnapshotKey = "current";

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        GetSnapshotAsync(cancellationToken);

    public Task<TipSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken) =>
        database.GetAsync<TipSnapshot>(StoreName, SnapshotKey, cancellationToken);

    public Task ReplaceAsync(TipSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return database.ReplaceAsync(
            StoreName,
            SnapshotKey,
            snapshot,
            cancellationToken);
    }
}

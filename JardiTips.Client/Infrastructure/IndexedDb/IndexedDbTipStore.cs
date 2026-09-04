using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Infrastructure.IndexedDb;

public sealed class IndexedDbTipStore(BrowserDatabase database) : ITipStore
{
    private const string StoreName = "tipSnapshots";
    private const string LegacySnapshotKey = "current";

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        database.GetAsync<TipSnapshot>(StoreName, LegacySnapshotKey, cancellationToken);

    public Task<TipSnapshot?> GetSnapshotAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(categoryId, Guid.Empty);

        return database.GetAsync<TipSnapshot>(StoreName, GetSnapshotKey(categoryId), cancellationToken);
    }

    public Task ReplaceAsync(TipSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return database.PutAsync(
            StoreName,
            GetSnapshotKey(snapshot.CategoryId),
            snapshot,
            cancellationToken);
    }

    private static string GetSnapshotKey(Guid categoryId) => categoryId.ToString("D");
}

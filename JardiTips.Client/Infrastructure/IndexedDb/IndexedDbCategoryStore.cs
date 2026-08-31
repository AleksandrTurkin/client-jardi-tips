using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Infrastructure.IndexedDb;

public sealed class IndexedDbCategoryStore(BrowserDatabase database) : ICategoryStore
{
    private const string StoreName = "categorySnapshots";
    private const string SnapshotKey = "current";

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        GetSnapshotAsync(cancellationToken);

    public Task<CategorySnapshot?> GetSnapshotAsync(CancellationToken cancellationToken) =>
        database.GetAsync<CategorySnapshot>(StoreName, SnapshotKey, cancellationToken);

    public Task ReplaceAsync(CategorySnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return database.ReplaceAsync(
            StoreName,
            SnapshotKey,
            snapshot,
            cancellationToken);
    }
}

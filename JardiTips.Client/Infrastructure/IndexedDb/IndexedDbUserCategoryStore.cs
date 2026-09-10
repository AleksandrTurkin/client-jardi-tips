using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Infrastructure.IndexedDb;

public sealed class IndexedDbUserCategoryStore(BrowserDatabase database) : IUserCategoryStore
{
    private const string StoreName = "userCategorySnapshots";

    public Task<UserCategorySnapshot?> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();

        return database.GetAsync<UserCategorySnapshot>(StoreName, userId, cancellationToken);
    }

    public Task ReplaceAsync(
        string userId,
        UserCategorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        return database.PutAsync(StoreName, userId, snapshot, cancellationToken);
    }
}

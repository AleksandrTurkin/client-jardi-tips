using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IUserCategoryStore
{
    Task<UserCategorySnapshot?> GetSnapshotAsync(string userId, CancellationToken cancellationToken);

    Task ReplaceAsync(
        string userId,
        UserCategorySnapshot snapshot,
        CancellationToken cancellationToken);
}

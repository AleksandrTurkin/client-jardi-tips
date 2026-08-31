using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ICategoryStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<CategorySnapshot?> GetSnapshotAsync(CancellationToken cancellationToken);

    Task ReplaceAsync(CategorySnapshot snapshot, CancellationToken cancellationToken);
}

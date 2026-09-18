using System.Security.Claims;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Coordination;

public sealed class CategoryLikeService(
    ICategoryApiSource apiSource,
    ICategoryStore store,
    IAuthenticationService authenticationService,
    ILogger<CategoryLikeService> logger) : ICategoryLikeService
{
    public async Task<CategoryDto> ToggleAsync(CategoryDto category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (!authenticationService.IsAuthenticated)
            throw new InvalidOperationException("Only authenticated users can like categories.");

        var updated = category with
        {
            IsLiked = !category.IsLiked,
            LikesCount = Math.Max(0, category.LikesCount + (category.IsLiked ? -1 : 1))
        };

        if (category.IsLiked)
            await apiSource.UnlikeAsync(category.Id, cancellationToken);
        else
            await apiSource.LikeAsync(category.Id, cancellationToken);

        await UpdateSnapshotAsync(category.Id, updated);

        return updated;
    }

    private async Task UpdateSnapshotAsync(Guid categoryId, CategoryDto updated)
    {
        try
        {
            var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
            if (snapshot is null)
                return;

            var categories = snapshot.Categories
                .Select(existing => existing.Id == categoryId ? updated : existing)
                .ToList();

            if (!categories.Any(existing => existing.Id == categoryId))
                return;

            await store.ReplaceAsync(snapshot with { Categories = categories }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Category like state could not be persisted to the local cache.");
        }
    }
}

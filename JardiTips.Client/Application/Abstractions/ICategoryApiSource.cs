using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ICategoryApiSource
{
    Task<PagedResult<CategoryDto>> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken);

    Task LikeAsync(Guid categoryId, CancellationToken cancellationToken);

    Task UnlikeAsync(Guid categoryId, CancellationToken cancellationToken);
}

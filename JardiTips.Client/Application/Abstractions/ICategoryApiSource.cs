using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ICategoryApiSource
{
    Task<PagedResult<CategoryDto>> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken);
}

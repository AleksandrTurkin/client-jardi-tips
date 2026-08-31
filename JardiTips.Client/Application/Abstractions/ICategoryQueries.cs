using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ICategoryQueries
{
    Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<CategoryDto>> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken = default);
}

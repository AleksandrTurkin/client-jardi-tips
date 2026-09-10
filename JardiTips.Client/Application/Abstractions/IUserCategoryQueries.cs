using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IUserCategoryQueries
{
    Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserCategoriesDto> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken = default);
}

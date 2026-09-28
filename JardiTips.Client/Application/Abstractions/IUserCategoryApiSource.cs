using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IUserCategoryApiSource
{
    Task<Guid> CreateAsync(string name, string description, CancellationToken cancellationToken);

    Task UpdateAsync(Guid id, string name, string description, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<UserCategoriesDto> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken);

    Task<UserCategoriesDto> GetAllAsync(CancellationToken cancellationToken);
}

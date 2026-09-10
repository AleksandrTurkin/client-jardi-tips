using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface IUserCategoryApiSource
{
    Task<UserCategoriesDto> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken);

    Task<UserCategoriesDto> GetAllAsync(CancellationToken cancellationToken);
}

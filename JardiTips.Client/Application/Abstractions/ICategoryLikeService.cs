using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ICategoryLikeService
{
    Task<CategoryDto> ToggleAsync(CategoryDto category, CancellationToken cancellationToken = default);
}

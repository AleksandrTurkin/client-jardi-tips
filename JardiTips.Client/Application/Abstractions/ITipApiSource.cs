using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ITipApiSource
{
    Task<PagedResult<TipDto>> GetAsync(
        TipsFilter filter,
        CancellationToken cancellationToken);
}

using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ITipApiSource
{
    Task<PagedResult<TipDto>> GetAsync(
        TipsFilter filter,
        CancellationToken cancellationToken);

    Task<TipCreateOutcome> CreateAsync(
        CreateTipRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(Guid id, UpdateTipRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

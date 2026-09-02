using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Application.Abstractions;

public interface ITipQueries
{
    Task<TipCollection> GetAsync(
        Guid categoryId,
        int lastVisibleIndex = -1,
        CancellationToken cancellationToken = default);
}

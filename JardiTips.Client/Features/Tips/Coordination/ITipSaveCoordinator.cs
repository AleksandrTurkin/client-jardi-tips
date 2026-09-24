using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Features.Tips.Coordination;

public interface ITipSaveCoordinator
{
    event Action? StateChanged;

    TipSaveState State { get; }

    bool IsAuthenticated { get; }

    Task ActivateAsync(CancellationToken cancellationToken = default);

    Task<TipSaveOutcome> SaveAsync(TipDto tip, CancellationToken cancellationToken = default);

    Task DeactivateAsync();
}

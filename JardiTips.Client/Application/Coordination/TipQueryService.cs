using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Application.Coordination;

public sealed class TipQueryService(
    ITipApiSource apiSource,
    ITipStore store,
    ILogger<TipQueryService> logger) : ITipQueries, IDisposable
{
    private const int PrefetchThreshold = 5;

    private readonly Lock initializationLock = new();
    private readonly SemaphoreSlim queryLock = new(1, 1);
    private Task? initializationTask;

    public async Task<TipCollection> GetAsync(
        Guid categoryId,
        int lastVisibleIndex = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(categoryId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(lastVisibleIndex, -1);

        await GetInitializationTask().WaitAsync(cancellationToken);
        await queryLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            if (snapshot is null || snapshot.CategoryId != categoryId)
            {
                snapshot = new TipSnapshot(
                    categoryId,
                    [],
                    null,
                    true,
                    DateTimeOffset.UtcNow);
            }

            if (snapshot.Tips.Count == 0 && snapshot.HasMore)
                snapshot = await LoadNextPageAsync(snapshot, cancellationToken);

            if (ShouldLoadNextPage(snapshot, lastVisibleIndex))
            {
                try
                {
                    snapshot = await LoadNextPageAsync(snapshot, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(
                        exception,
                        "Tip cache continuation failed for category {CategoryId}; cached tips were preserved.",
                        categoryId);
                }
            }

            return new TipCollection(snapshot.Tips, snapshot.HasMore);
        }
        finally
        {
            queryLock.Release();
        }
    }

    private Task GetInitializationTask()
    {
        lock (initializationLock)
            return initializationTask ??= store.InitializeAsync(CancellationToken.None);
    }

    private static bool ShouldLoadNextPage(TipSnapshot snapshot, int lastVisibleIndex) =>
        snapshot.HasMore
        && lastVisibleIndex >= 0
        && snapshot.Tips.Count - lastVisibleIndex - 1 <= PrefetchThreshold;

    private async Task<TipSnapshot> LoadNextPageAsync(
        TipSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var page = await apiSource.GetAsync(
            new TipsFilter(snapshot.CategoryId, snapshot.PageContext),
            cancellationToken);
        var existingIds = snapshot.Tips.Select(tip => tip.Id).ToHashSet();
        var tips = snapshot.Tips
            .Concat(page.Data.Where(tip => existingIds.Add(tip.Id)))
            .ToList();
        var pageContext = string.IsNullOrWhiteSpace(page.PageContext)
            ? null
            : page.PageContext;
        var hasMore = pageContext is not null
            && !string.Equals(pageContext, snapshot.PageContext, StringComparison.Ordinal);
        var updatedSnapshot = new TipSnapshot(
            snapshot.CategoryId,
            tips,
            pageContext,
            hasMore,
            DateTimeOffset.UtcNow);

        await store.ReplaceAsync(updatedSnapshot, cancellationToken);
        return updatedSnapshot;
    }

    public void Dispose() => queryLock.Dispose();
}

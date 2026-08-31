using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Application.Coordination;

public sealed class CategoryQueryService(
    ICategoryApiSource apiSource,
    ICategoryStore store,
    ILogger<CategoryQueryService> logger) : ICategoryQueries, ICategoryStartup
{
    private const int DefaultPageSize = 15;
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;
    private const string PageContextPrefix = "offset:";

    private readonly Lock synchronizationLock = new();
    private Task? initializationTask;
    private Task? sessionSynchronizationTask;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await GetInitializationTask().WaitAsync(cancellationToken);
        StartSessionSynchronization();
    }

    public async Task<CategoryDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var snapshot = await store.GetSnapshotAsync(cancellationToken);

        if (snapshot is not null)
        {
            StartSessionSynchronization();
            return snapshot.Categories.FirstOrDefault(category => category.Id == id);
        }

        await SynchronizeAsync(cancellationToken);
        snapshot = await store.GetSnapshotAsync(cancellationToken);
        return snapshot?.Categories.FirstOrDefault(category => category.Id == id);
    }

    public async Task<PagedResult<CategoryDto>> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await InitializeAsync(cancellationToken);

        var snapshot = await store.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            await SynchronizeAsync(cancellationToken);
            snapshot = await store.GetSnapshotAsync(cancellationToken);
        }
        else
        {
            StartSessionSynchronization();
        }

        var limit = Math.Clamp(
            filter.Limit ?? DefaultPageSize,
            MinimumPageSize,
            MaximumPageSize);
        var offset = ParseOffset(filter.PageContext);
        var data = snapshot?.Categories.Skip(offset).Take(limit).ToList() ?? [];
        var nextOffset = offset + data.Count;
        var pageContext = snapshot is not null && nextOffset < snapshot.Categories.Count
            ? $"offset:{nextOffset}"
            : null;

        return new PagedResult<CategoryDto>(pageContext, data);
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        await GetInitializationTask().WaitAsync(cancellationToken);
        await GetSessionSynchronizationTask().WaitAsync(cancellationToken);
    }

    private Task GetInitializationTask()
    {
        lock (synchronizationLock)
            return initializationTask ??= store.InitializeAsync(CancellationToken.None);
    }

    private Task GetSessionSynchronizationTask()
    {
        lock (synchronizationLock)
        {
            if (sessionSynchronizationTask is null)
            {
                sessionSynchronizationTask = SynchronizeCoreAsync();
                _ = ObserveSynchronizationAsync(sessionSynchronizationTask);
            }

            return sessionSynchronizationTask;
        }
    }

    private void StartSessionSynchronization()
    {
        _ = GetSessionSynchronizationTask();
    }

    private async Task ObserveSynchronizationAsync(Task synchronizationTask)
    {
        try
        {
            await synchronizationTask;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Category cache synchronization failed; the existing snapshot was preserved.");
        }
    }

    private async Task SynchronizeCoreAsync()
    {
        var categories = await apiSource.GetAllAsync(CancellationToken.None);
        await store.ReplaceAsync(
            new CategorySnapshot(categories, DateTimeOffset.UtcNow),
            CancellationToken.None);
    }

    private static int ParseOffset(string? pageContext)
    {
        return pageContext is not null
            && pageContext.StartsWith(PageContextPrefix, StringComparison.Ordinal)
            && int.TryParse(pageContext.AsSpan(PageContextPrefix.Length), out var offset)
            && offset >= 0
            ? offset
            : 0;
    }

}

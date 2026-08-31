using Microsoft.JSInterop;

namespace JardiTips.Client.Infrastructure.IndexedDb;

public sealed class BrowserDatabase(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const string ModulePath = "./indexedDb.js";

    private readonly SemaphoreSlim moduleLock = new(1, 1);
    private IJSObjectReference? module;

    public async Task<T?> GetAsync<T>(
        string storeName,
        string key,
        CancellationToken cancellationToken)
    {
        var databaseModule = await GetModuleAsync(cancellationToken);
        return await databaseModule.InvokeAsync<T?>(
            "get",
            cancellationToken,
            storeName,
            key);
    }

    public async Task ReplaceAsync<T>(
        string storeName,
        string key,
        T value,
        CancellationToken cancellationToken)
    {
        var databaseModule = await GetModuleAsync(cancellationToken);
        await databaseModule.InvokeVoidAsync(
            "replace",
            cancellationToken,
            storeName,
            key,
            value);
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (module is not null)
            return module;

        await moduleLock.WaitAsync(cancellationToken);
        try
        {
            module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                ModulePath);

            return module;
        }
        finally
        {
            moduleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
            await module.DisposeAsync();

        moduleLock.Dispose();
    }
}

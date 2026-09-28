using JardiTips.Client.Application.Abstractions;
using Microsoft.JSInterop;

namespace JardiTips.Client.Infrastructure.Browser;

public sealed class BrowserConnectivity(IJSRuntime jsRuntime) : IBrowserConnectivity, IAsyncDisposable
{
    private const string ModulePath = "./connectivity.js";

    private readonly SemaphoreSlim moduleLock = new(1, 1);
    private IJSObjectReference? module;
    private DotNetObjectReference<BrowserConnectivity>? reference;
    private bool subscribed;
    private bool disposed;

    public event Action<bool>? ConnectivityChanged;

    public bool IsOnline { get; private set; } = true;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (subscribed)
            return;

        var connectivityModule = await GetModuleAsync(cancellationToken);
        reference = DotNetObjectReference.Create(this);

        IsOnline = await connectivityModule.InvokeAsync<bool>(
            "subscribe",
            cancellationToken,
            reference);
        subscribed = true;
    }

    [JSInvokable]
    public void OnConnectivityChanged(bool isOnline)
    {
        if (IsOnline == isOnline)
            return;

        IsOnline = isOnline;
        ConnectivityChanged?.Invoke(isOnline);
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
        if (disposed)
            return;

        disposed = true;
        ConnectivityChanged = null;

        if (subscribed && module is not null && reference is not null)
        {
            try
            {
                await module.InvokeVoidAsync("unsubscribe", reference);
            }
            catch (JSDisconnectedException)
            {
            }
        }

        reference?.Dispose();

        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        moduleLock.Dispose();
    }
}

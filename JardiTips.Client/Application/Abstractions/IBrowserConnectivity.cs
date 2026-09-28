namespace JardiTips.Client.Application.Abstractions;

public interface IBrowserConnectivity
{
    event Action<bool>? ConnectivityChanged;

    bool IsOnline { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
}

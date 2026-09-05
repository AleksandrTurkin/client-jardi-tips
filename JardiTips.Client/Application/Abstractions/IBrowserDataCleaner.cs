namespace JardiTips.Client.Application.Abstractions;

public interface IBrowserDataCleaner
{
    Task ClearAsync(CancellationToken cancellationToken);
}

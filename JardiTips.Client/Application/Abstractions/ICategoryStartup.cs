namespace JardiTips.Client.Application.Abstractions;

public interface ICategoryStartup
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

namespace JardiTips.Client.Application.Abstractions;

public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}

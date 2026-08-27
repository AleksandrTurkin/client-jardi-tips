namespace JardiTips.Client.Application.Abstractions;

public sealed class AnonymousAccessTokenProvider : IAccessTokenProvider
{
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }
}

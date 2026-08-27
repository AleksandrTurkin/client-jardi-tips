namespace JardiTips.Client.Application.Abstractions;

public interface IApiClient
{
    Task<TResponse> GetAsync<TResponse>(string route, CancellationToken cancellationToken);

    Task GetAsync(string route, CancellationToken cancellationToken);

    Task<TResponse> PostAsync<TRequest, TResponse>(
        string route,
        TRequest request,
        CancellationToken cancellationToken);

    Task PostAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken);

    Task<TResponse> PutAsync<TRequest, TResponse>(
        string route,
        TRequest request,
        CancellationToken cancellationToken);

    Task PutAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken);

    Task<TResponse> DeleteAsync<TResponse>(string route, CancellationToken cancellationToken);

    Task DeleteAsync(string route, CancellationToken cancellationToken);
}

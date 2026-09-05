using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JardiTips.Client.Application.Abstractions;

namespace JardiTips.Client.Infrastructure.Api;

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _accessTokenProvider;

    public ApiClient(HttpClient httpClient, IAccessTokenProvider accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
    }

    public Task<TResponse> GetAsync<TResponse>(
        string route,
        CancellationToken cancellationToken) =>
        SendForResponseAsync<TResponse>(HttpMethod.Get, route, content: null, cancellationToken);

    public Task GetAsync(string route, CancellationToken cancellationToken) =>
        SendWithoutResponseAsync(HttpMethod.Get, route, content: null, cancellationToken);

    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string route,
        TRequest request,
        CancellationToken cancellationToken) =>
        SendForResponseAsync<TResponse>(
            HttpMethod.Post,
            route,
            CreateJsonContent(request),
            cancellationToken);

    public Task PostAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken) =>
        SendWithoutResponseAsync(
            HttpMethod.Post,
            route,
            CreateJsonContent(request),
            cancellationToken);

    public Task<TResponse> PutAsync<TRequest, TResponse>(
        string route,
        TRequest request,
        CancellationToken cancellationToken) =>
        SendForResponseAsync<TResponse>(
            HttpMethod.Put,
            route,
            CreateJsonContent(request),
            cancellationToken);

    public Task PutAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken) =>
        SendWithoutResponseAsync(
            HttpMethod.Put,
            route,
            CreateJsonContent(request),
            cancellationToken);

    public Task<TResponse> DeleteAsync<TResponse>(
        string route,
        CancellationToken cancellationToken) =>
        SendForResponseAsync<TResponse>(HttpMethod.Delete, route, content: null, cancellationToken);

    public Task DeleteAsync(string route, CancellationToken cancellationToken) =>
        SendWithoutResponseAsync(HttpMethod.Delete, route, content: null, cancellationToken);

    private async Task<TResponse> SendForResponseAsync<TResponse>(
        HttpMethod method,
        string route,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, route, content);
        using var response = await SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default!;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions)!;
    }

    private async Task SendWithoutResponseAsync(
        HttpMethod method,
        string route,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, route, content);
        using var response = await SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string route,
        HttpContent? content)
    {
        var request = new HttpRequestMessage(method, CreateRelativeUri(route))
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
            return await SendCoreAsync(request, cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var retryRequest = await CloneRequestAsync(request, cancellationToken);
        var response = await SendCoreAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        string? refreshedAccessToken;
        try
        {
            refreshedAccessToken = await _accessTokenProvider.RefreshAccessTokenAsync(cancellationToken);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
            return response;

        response.Dispose();
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            refreshedAccessToken.Trim());

        return await SendCoreAsync(retryRequest, cancellationToken);
    }

    private Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var content = new ByteArrayContent(
                await request.Content.ReadAsByteArrayAsync(cancellationToken));

            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);

            clone.Content = content;
        }

        return clone;
    }

    internal static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var problemDetails = ParseProblemDetails(response.StatusCode, response.ReasonPhrase, responseBody);
        throw new ApiException(response.StatusCode, problemDetails);
    }

    private static ApiProblemDetails ParseProblemDetails(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string responseBody)
    {
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var parsedProblem = JsonSerializer.Deserialize<ApiProblemDetails>(
                    responseBody,
                    JsonOptions);

                if (parsedProblem is not null)
                {
                    return parsedProblem.Status is null
                        ? parsedProblem with { Status = (int)statusCode }
                        : parsedProblem;
                }
            }
            catch (JsonException)
            {
                return CreateFallbackProblem(statusCode, reasonPhrase, responseBody);
            }
        }

        return CreateFallbackProblem(statusCode, reasonPhrase, responseBody);
    }

    private static ApiProblemDetails CreateFallbackProblem(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string responseBody)
    {
        return new ApiProblemDetails
        {
            Status = (int)statusCode,
            Title = reasonPhrase ?? "API request failed",
            Detail = string.IsNullOrWhiteSpace(responseBody) ? null : responseBody
        };
    }

    private static HttpContent CreateJsonContent<TRequest>(TRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static Uri CreateRelativeUri(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("An API route is required.", nameof(route));
        }

        if (Uri.TryCreate(route, UriKind.Absolute, out _))
        {
            throw new ArgumentException("API routes must be relative to the configured API base URL.", nameof(route));
        }

        return new Uri(route.TrimStart('/'), UriKind.Relative);
    }
}

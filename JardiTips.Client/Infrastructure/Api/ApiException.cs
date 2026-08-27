using System.Net;

namespace JardiTips.Client.Infrastructure.Api;

public sealed class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, ApiProblemDetails problemDetails)
        : base(CreateMessage(statusCode, problemDetails))
    {
        StatusCode = statusCode;
        ProblemDetails = problemDetails;
    }

    public HttpStatusCode StatusCode { get; }

    public int HttpStatus => (int)StatusCode;

    public ApiProblemDetails ProblemDetails { get; }

    private static string CreateMessage(
        HttpStatusCode statusCode,
        ApiProblemDetails problemDetails)
    {
        var message = problemDetails.Detail ?? problemDetails.Title;
        return string.IsNullOrWhiteSpace(message)
            ? $"The API request failed with HTTP {(int)statusCode} ({statusCode})."
            : $"The API request failed with HTTP {(int)statusCode} ({statusCode}): {message}";
    }
}

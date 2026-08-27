namespace JardiTips.Client.Infrastructure.Api;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = string.Empty;

    public Uri CreateBaseUri()
    {
        if (!TryCreateBaseUri(BaseUrl, out var baseUri) || baseUri is null)
        {
            throw new InvalidOperationException(
                $"Configuration value '{SectionName}:BaseUrl' must be an absolute HTTP or HTTPS URL.");
        }

        return baseUri;
    }

    public static bool TryCreateBaseUri(string? baseUrl, out Uri? baseUri)
    {
        baseUri = null;

        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedUri)
            || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(parsedUri.UserInfo)
            || !string.IsNullOrEmpty(parsedUri.Query)
            || !string.IsNullOrEmpty(parsedUri.Fragment))
        {
            return false;
        }

        var path = parsedUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? parsedUri.AbsolutePath
            : $"{parsedUri.AbsolutePath}/";

        var builder = new UriBuilder(parsedUri)
        {
            Path = path
        };

        baseUri = builder.Uri;
        return true;
    }
}

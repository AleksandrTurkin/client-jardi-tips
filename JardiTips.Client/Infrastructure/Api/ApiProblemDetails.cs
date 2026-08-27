using System.Text.Json;
using System.Text.Json.Serialization;

namespace JardiTips.Client.Infrastructure.Api;

public sealed record ApiProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]> Errors { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }
}

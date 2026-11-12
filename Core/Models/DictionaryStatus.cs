#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Pronunciation dictionary state from <c>GET /api/health</c>.
/// </summary>
public sealed record DictionaryStatus
{
    /// <summary>Number of loaded dictionary rules.</summary>
    [JsonPropertyName("rules")]
    public int Rules { get; init; }

    /// <summary>Load error text, or <see langword="null"/> when the dictionary is loaded.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

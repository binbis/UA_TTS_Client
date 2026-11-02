#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Response of the endpoints that return text: <c>POST /api/verbalize</c> and <c>POST /api/parse_file</c>.
/// </summary>
public sealed record TextResponseDto
{
    /// <summary>Text returned by the server.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

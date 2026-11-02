#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Error body returned by the TTS server in JSON format.
/// </summary>
/// <remarks>
/// The server has historically also replied with plain text (<c>No file</c>), so the client must
/// tolerate both formats. This DTO describes only the JSON variant.
/// </remarks>
public sealed record ApiErrorResponse
{
    /// <summary>Error description from the server.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Response of <c>GET /api/health</c>.
/// </summary>
public sealed record ServerHealth
{
    /// <summary>Server status; expected to be <c>ok</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    /// <summary>Compute device: <c>cuda</c> or <c>cpu</c>.</summary>
    [JsonPropertyName("device")]
    public string? Device { get; init; }

    /// <summary>Number of loaded voices.</summary>
    [JsonPropertyName("voices")]
    public int Voices { get; init; }

    /// <summary>Identifier of the synthesis model.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

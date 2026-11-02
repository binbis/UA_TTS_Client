#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Request body for <c>POST /api/verbalize</c>.
/// </summary>
public sealed record VerbalizeRequestDto
{
    /// <summary>Text to verbalize (conversion of numbers and abbreviations into words).</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

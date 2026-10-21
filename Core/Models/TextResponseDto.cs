#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Відповідь ендпоінтів, що повертають текст: <c>POST /api/verbalize</c> і <c>POST /api/parse_file</c>.
/// </summary>
public sealed record TextResponseDto
{
    /// <summary>Текст, який повернув сервер.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

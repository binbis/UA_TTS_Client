#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Тіло запиту до <c>POST /api/verbalize</c>.
/// </summary>
public sealed record VerbalizeRequestDto
{
    /// <summary>Текст для вербалізації (перетворення чисел і скорочень у слова).</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

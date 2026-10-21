#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Тіло запиту до <c>POST /api/synthesize</c>.
/// </summary>
/// <remarks>
/// Імена JSON-властивостей зафіксовані сервером: <c>text</c>, <c>voice</c>, <c>speed</c>,
/// <c>volume</c>, <c>translate</c>.
/// </remarks>
public sealed record TtsRequestDto
{
    /// <summary>Текст, який потрібно озвучити. Обов'язковий.</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>Ім'я голосу зі списку <c>GET /api/voices</c>. Якщо <see langword="null"/>, сервер бере перший доступний.</summary>
    [JsonPropertyName("voice")]
    public string? Voice { get; init; }

    /// <summary>Швидкість мовлення; 1.0 — звичайна.</summary>
    [JsonPropertyName("speed")]
    public double Speed { get; init; } = 1.0;

    /// <summary>Підсилення амплітуди на боці сервера; 1.0 — без змін.</summary>
    [JsonPropertyName("volume")]
    public double Volume { get; init; } = 1.0;

    /// <summary>Чи перекладати текст українською перед синтезом.</summary>
    [JsonPropertyName("translate")]
    public bool Translate { get; init; }
}

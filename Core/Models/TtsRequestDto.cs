#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Request body for <c>POST /api/synthesize</c>.
/// </summary>
/// <remarks>
/// The JSON property names are fixed by the server: <c>text</c>, <c>voice</c>, <c>speed</c>,
/// <c>volume</c>, <c>translate</c>.
/// </remarks>
public sealed record TtsRequestDto
{
    /// <summary>Text to speak. Required.</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>Voice name from the <c>GET /api/voices</c> list. If <see langword="null"/>, the server uses the first available voice.</summary>
    [JsonPropertyName("voice")]
    public string? Voice { get; init; }

    /// <summary>Speech speed; 1.0 is normal.</summary>
    [JsonPropertyName("speed")]
    public double Speed { get; init; } = 1.0;

    /// <summary>Amplitude gain on the server side; 1.0 means no change.</summary>
    [JsonPropertyName("volume")]
    public double Volume { get; init; } = 1.0;

    /// <summary>Whether to translate the text into Ukrainian before synthesis.</summary>
    [JsonPropertyName("translate")]
    public bool Translate { get; init; }
}

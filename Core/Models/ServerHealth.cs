#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Відповідь <c>GET /api/health</c>.
/// </summary>
public sealed record ServerHealth
{
    /// <summary>Стан сервера; очікується <c>ok</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    /// <summary>Пристрій обчислень: <c>cuda</c> або <c>cpu</c>.</summary>
    [JsonPropertyName("device")]
    public string? Device { get; init; }

    /// <summary>Кількість завантажених голосів.</summary>
    [JsonPropertyName("voices")]
    public int Voices { get; init; }

    /// <summary>Ідентифікатор моделі синтезу.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

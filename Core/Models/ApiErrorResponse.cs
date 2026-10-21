#nullable enable

using System.Text.Json.Serialization;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Тіло помилки, яке повертає TTS-сервер у форматі JSON.
/// </summary>
/// <remarks>
/// Сервер історично відповідав і plain text (<c>No file</c>), тому клієнт зобов'язаний
/// терпіти обидва формати. Цей DTO описує лише JSON-варіант.
/// </remarks>
public sealed record ApiErrorResponse
{
    /// <summary>Опис помилки від сервера.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

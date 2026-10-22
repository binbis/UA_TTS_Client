#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Параметри експорту озвученого тексту у файл.
/// </summary>
public sealed record AudioExportRequest
{
    /// <summary>Повний текст для синтезу й збереження.</summary>
    public required string Text { get; init; }

    /// <summary>Ім'я голосу; <see langword="null"/> — голос за замовчуванням сервера.</summary>
    public string? Voice { get; init; }

    /// <summary>Швидкість мовлення; 1.0 — звичайна.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Параметри поділу тексту на фрагменти.</summary>
    public TextChunkingOptions Chunking { get; init; } = new();

    /// <summary>Формат збереження.</summary>
    public AudioExportFormat Format { get; init; } = AudioExportFormat.Mp4;

    /// <summary>Шлях до файлу, який буде створено (перезаписується атомарно).</summary>
    public required string DestinationPath { get; init; }
}

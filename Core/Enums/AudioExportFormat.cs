#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Формат збереження озвученого тексту у файл.
/// </summary>
public enum AudioExportFormat
{
    /// <summary>Аудіо без відео (AAC у контейнері MP4). Типовий формат.</summary>
    Mp4,

    /// <summary>WAV без стиснення (24 кГц, моно, 16-bit PCM).</summary>
    Wav
}

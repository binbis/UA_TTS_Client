#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Format for saving spoken text to a file.
/// </summary>
public enum AudioExportFormat
{
    /// <summary>MP3 audio. The default format.</summary>
    Mp3,

    /// <summary>Uncompressed WAV (24 kHz, mono, 16-bit PCM).</summary>
    Wav
}

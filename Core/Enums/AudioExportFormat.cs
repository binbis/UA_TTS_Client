#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Format for saving spoken text to a file.
/// </summary>
public enum AudioExportFormat
{
    /// <summary>Audio without video (AAC in an MP4 container). The default format.</summary>
    Mp4,

    /// <summary>Uncompressed WAV (24 kHz, mono, 16-bit PCM).</summary>
    Wav
}

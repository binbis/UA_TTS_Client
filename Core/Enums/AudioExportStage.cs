#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Stages of exporting spoken text to a file.
/// </summary>
public enum AudioExportStage
{
    /// <summary>Sequential synthesis of the text fragments.</summary>
    Synthesizing,

    /// <summary>Concatenation of WAV fragments into a single stream.</summary>
    Concatenating,

    /// <summary>Conversion of WAV to the selected format (MP3).</summary>
    Converting,

    /// <summary>Writing the result to disk.</summary>
    Writing
}

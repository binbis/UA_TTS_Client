#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// How text is split into fragments before synthesis.
/// </summary>
public enum TextChunkingMode
{
    /// <summary>Each paragraph is a separate fragment; overly long paragraphs are additionally split by sentences.</summary>
    Paragraphs,

    /// <summary>Each sentence is a separate fragment.</summary>
    Sentences,

    /// <summary>Sentences are packed into fragments of up to 250 characters, without breaking up sentences.</summary>
    MaxLength
}

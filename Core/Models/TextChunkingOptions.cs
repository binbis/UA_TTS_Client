#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Text chunking options.
/// </summary>
public sealed record TextChunkingOptions
{
    /// <summary>Default maximum fragment length (in characters).</summary>
    public const int DefaultMaxChunkLength = 250;

    /// <summary>Chunking mode (default: paragraphs).</summary>
    public TextChunkingMode Mode { get; init; } = TextChunkingMode.Paragraphs;

    /// <summary>Maximum fragment length in characters.</summary>
    public int MaxChunkLength { get; init; } = DefaultMaxChunkLength;
}

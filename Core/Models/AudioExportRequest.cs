#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Parameters for exporting spoken text to a file.
/// </summary>
public sealed record AudioExportRequest
{
    /// <summary>Full text to synthesize and save.</summary>
    public required string Text { get; init; }

    /// <summary>Voice name; <see langword="null"/> means the server's default voice.</summary>
    public string? Voice { get; init; }

    /// <summary>Speech speed; 1.0 is normal.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Text chunking options.</summary>
    public TextChunkingOptions Chunking { get; init; } = new();

    /// <summary>Save format.</summary>
    public AudioExportFormat Format { get; init; } = AudioExportFormat.Mp3;

    /// <summary>Path of the file to be created (overwritten atomically).</summary>
    public required string DestinationPath { get; init; }
}

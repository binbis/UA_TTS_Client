#nullable enable

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Options for the fragment playback queue.
/// </summary>
public sealed record SpeechQueueOptions
{
    /// <summary>Minimum number of fragments in a batch.</summary>
    public const int MinBatchSize = 1;

    /// <summary>Maximum number of fragments in a batch.</summary>
    public const int MaxBatchSize = 10;

    /// <summary>Default number of fragments in a batch.</summary>
    public const int DefaultBatchSize = 5;

    /// <summary>Voice name, or <see langword="null"/> for the default voice.</summary>
    public string? Voice { get; init; }

    /// <summary>Speech speed passed to the server.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Number of fragments in one batch (from 1 to 10).</summary>
    public int BatchSize { get; init; } = DefaultBatchSize;

    /// <summary>
    /// Whether to stop playback after each batch (default) or play all batches in a row.
    /// </summary>
    public bool PauseAfterEachBatch { get; init; } = true;
}

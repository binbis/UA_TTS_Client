#nullable enable

using System;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Data for the event raised when a batch of fragments completes.
/// </summary>
/// <param name="completedBatch">Number of the completed batch (from 1).</param>
/// <param name="totalBatches">Total number of batches.</param>
/// <param name="hasNext">Whether there is a next batch to continue with.</param>
public sealed class BatchCompletedEventArgs(int completedBatch, int totalBatches, bool hasNext) : EventArgs
{
    /// <summary>Number of the completed batch (from 1).</summary>
    public int CompletedBatch { get; } = completedBatch;

    /// <summary>Total number of batches.</summary>
    public int TotalBatches { get; } = totalBatches;

    /// <summary>Whether there is a next batch to continue with.</summary>
    public bool HasNext { get; } = hasNext;
}

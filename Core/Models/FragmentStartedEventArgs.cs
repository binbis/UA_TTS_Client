#nullable enable

using System;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Data for the event raised when fragment playback starts.
/// </summary>
/// <param name="fragment">The fragment that started playing.</param>
/// <param name="batch">Number of the current batch (from 1).</param>
/// <param name="totalBatches">Total number of batches.</param>
public sealed class FragmentStartedEventArgs(TextFragment fragment, int batch, int totalBatches) : EventArgs
{
    /// <summary>The fragment that started playing.</summary>
    public TextFragment Fragment { get; } = fragment ?? throw new ArgumentNullException(nameof(fragment));

    /// <summary>Number of the current batch (from 1).</summary>
    public int Batch { get; } = batch;

    /// <summary>Total number of batches.</summary>
    public int TotalBatches { get; } = totalBatches;
}

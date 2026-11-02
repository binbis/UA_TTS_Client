#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Synthesizes and plays fragments sequentially, in batches of N.
/// </summary>
/// <remarks>
/// <para><see cref="StartAsync"/> returns as soon as the first fragment has started playing,
/// not after the whole batch is finished; otherwise the UI would be "busy" for minutes.</para>
/// <para>If <see cref="SpeechQueueOptions.PauseAfterEachBatch"/> is enabled (default),
/// playback stops between batches and the next batch is started by
/// <see cref="ContinueAsync"/>; otherwise all batches play one after another without intervention.</para>
/// </remarks>
public interface IFragmentPlaybackService
{
    /// <summary>Raised when the next fragment starts playing.</summary>
    event EventHandler<FragmentStartedEventArgs>? FragmentStarted;

    /// <summary>
    /// Raised after a batch completes if more batches remain and the pause between batches is enabled
    /// (<see cref="SpeechQueueOptions.PauseAfterEachBatch"/>).
    /// </summary>
    event EventHandler<BatchCompletedEventArgs>? BatchCompleted;

    /// <summary>Raised after the last fragment of the queue has been played.</summary>
    event EventHandler? QueueCompleted;

    /// <summary>Raised if the queue stopped with an error after it had started.</summary>
    event EventHandler<Exception>? Failed;

    /// <summary>Whether an active queue exists (playing, paused, or waiting for the next batch).</summary>
    bool HasQueue { get; }

    /// <summary>Whether the batch has completed and the queue is waiting for <see cref="ContinueAsync"/>.</summary>
    bool IsBatchPending { get; }

    /// <summary>Number of the fragment currently playing (from 1; 0 if nothing is playing).</summary>
    int CurrentFragment { get; }

    /// <summary>Total number of fragments in the queue.</summary>
    int TotalFragments { get; }

    /// <summary>Number of the current batch (from 1).</summary>
    int CurrentBatch { get; }

    /// <summary>Total number of batches.</summary>
    int TotalBatches { get; }

    /// <summary>
    /// Starts the queue: synthesizes fragments sequentially, in batches of
    /// <see cref="SpeechQueueOptions.BatchSize"/>, and starts playback.
    /// </summary>
    /// <param name="fragments">Fragments in playback order.</param>
    /// <param name="options">Voice, speed, and batch size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completes when the first fragment has started playing, or with a startup error.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The fragment list is empty.</exception>
    /// <exception cref="UA_TTS_Client.Core.Exceptions.TtsApiException">The server could not synthesize the first fragment.</exception>
    Task StartAsync(
        IReadOnlyList<TextFragment> fragments,
        SpeechQueueOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the next batch after <see cref="BatchCompleted"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completes when the first fragment of the new batch has started playing.</returns>
    Task ContinueAsync(CancellationToken cancellationToken = default);

    /// <summary>Pauses the current fragment and the queue.</summary>
    void Pause();

    /// <summary>Resumes playback after <see cref="Pause"/>.</summary>
    void Resume();

    /// <summary>Stops the queue, cancels synthesis, and resets the counters.</summary>
    void Stop();
}

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Plays audio from memory (without writing temporary files to disk).
/// </summary>
public interface IAudioPlayerService
{
    /// <summary>Current playback state.</summary>
    PlaybackState State { get; }

    /// <summary>Current volume in the range from 0.0 to 1.0.</summary>
    double Volume { get; }

    /// <summary>Raised every time <see cref="State"/> changes.</summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// Raised when the stream ends naturally.
    /// </summary>
    /// <remarks>
    /// Unlike the transition to <see cref="PlaybackState.Stopped"/> via <see cref="Stop"/>,
    /// this event means the track was played to the end. It is used by the fragment queue
    /// to wait for the current fragment to finish before the next one.
    /// </remarks>
    event EventHandler? TrackEnded;

    /// <summary>
    /// Loads the WAV data into memory and starts playback.
    /// </summary>
    /// <param name="wavData">Complete contents of the WAV file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="wavData"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="wavData"/> is empty.</exception>
    Task PlayAsync(byte[] wavData, CancellationToken cancellationToken = default);

    /// <summary>Pauses playback, keeping the current position.</summary>
    void Pause();

    /// <summary>Resumes playback after <see cref="Pause"/>.</summary>
    void Resume();

    /// <summary>Stops playback and releases the stream.</summary>
    void Stop();

    /// <summary>
    /// Sets the playback volume.
    /// </summary>
    /// <param name="volume">Value from 0.0 to 1.0; values outside this range are clamped.</param>
    void SetVolume(double volume);
}

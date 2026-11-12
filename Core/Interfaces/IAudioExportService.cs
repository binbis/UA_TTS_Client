#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Exports spoken text to an audio file (MP3 or WAV).
/// </summary>
/// <remarks>
/// The service does not depend on the UI: the file selection dialog is invoked by the ViewModel,
/// and a ready path is passed here. Synthesis runs sequentially so as not to overload the GPU.
/// </remarks>
public interface IAudioExportService
{
    /// <summary>
    /// Synthesizes the entire text, concatenates the fragments, and writes the file in the selected format.
    /// </summary>
    /// <param name="request">Export parameters.</param>
    /// <param name="progress">Progress receiver, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The text is empty or contains no fragments.</exception>
    /// <exception cref="InvalidDataException">The WAV fragments have an unexpected format.</exception>
    /// <exception cref="TtsApiException">The server is unavailable or returned an error.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    Task ExportAsync(
        AudioExportRequest request,
        IProgress<AudioExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

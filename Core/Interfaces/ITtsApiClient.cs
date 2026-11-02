#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Client for the HTTP API of the TTS server.
/// </summary>
/// <remarks>
/// All methods convert transport failures into <see cref="TtsApiException"/>,
/// so they can be called directly from the ViewModel.
/// </remarks>
public interface ITtsApiClient
{
    /// <summary>
    /// Gets the list of available voices (<c>GET /api/voices</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of voice names.</returns>
    /// <exception cref="TtsApiException">The server is unavailable or returned an error.</exception>
    Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the server status (<c>GET /api/health</c>).
    /// </summary>
    /// <remarks>
    /// If the server is an older version without <c>/api/health</c> (404), the method automatically checks
    /// availability via <c>GET /api/voices</c> and returns a synthetic response.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Server status.</returns>
    /// <exception cref="TtsApiException">The server is unavailable or returned an error.</exception>
    Task<ServerHealth> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verbalizes text — converts numbers and abbreviations into words (<c>POST /api/verbalize</c>).
    /// </summary>
    /// <param name="text">Input text.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Text after verbalization.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is empty.</exception>
    /// <exception cref="TtsApiException">The server is unavailable or returned an error.</exception>
    Task<string> VerbalizeAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synthesizes speech (<c>POST /api/synthesize</c>).
    /// </summary>
    /// <param name="request">Synthesis parameters.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Contents of the WAV file (24 kHz, mono, 16-bit PCM) in memory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="TtsApiException">The server is unavailable or returned an error.</exception>
    Task<byte[]> SynthesizeAsync(TtsRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts WAV to the specified format (<c>POST /api/convert</c>).
    /// </summary>
    /// <param name="wavData">Contents of the WAV file.</param>
    /// <param name="format">Target format, for example <c>mp4</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Contents of the converted file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wavData"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="format"/> is empty.</exception>
    /// <exception cref="TtsApiException">The server is unavailable or returned an error.</exception>
    Task<byte[]> ConvertAsync(byte[] wavData, string format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts text from a document file (<c>POST /api/parse_file</c>).
    /// </summary>
    /// <param name="fileStream">Stream with the file contents. The stream is disposed after the call completes.</param>
    /// <param name="fileName">File name including the extension (<c>.pdf</c>, <c>.docx</c>, <c>.odt</c>, <c>.txt</c>).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Extracted text.</returns>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    /// <exception cref="TtsApiException">The server is unavailable or returned an error.</exception>
    Task<string> ParseFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}

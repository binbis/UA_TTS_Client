#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Infrastructure.Audio;

/// <summary>
/// Exports spoken text to a file: sequential synthesis → WAV concatenation →
/// conversion (MP3) → atomic write.
/// </summary>
public sealed class AudioExportService : IAudioExportService
{
    private const string Mp3Format = "mp3";

    private readonly ITtsApiClient _apiClient;
    private readonly ITextChunker _textChunker;
    private readonly ILogger<AudioExportService> _logger;

    /// <summary>
    /// Creates the export service.
    /// </summary>
    /// <param name="apiClient">TTS server client.</param>
    /// <param name="textChunker">Service that splits text into fragments.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    public AudioExportService(
        ITtsApiClient apiClient,
        ITextChunker textChunker,
        ILogger<AudioExportService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _textChunker = textChunker ?? throw new ArgumentNullException(nameof(textChunker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ExportAsync(
        AudioExportRequest request,
        IProgress<AudioExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Text);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationPath);

        var fragments = _textChunker.Split(request.Text, request.Chunking);
        if (fragments.Count == 0)
        {
            throw new ArgumentException("Немає тексту для збереження.", nameof(request));
        }

        _logger.LogInformation("Експорт {Count} фрагментів у формат {Format}: {Path}",
            fragments.Count, request.Format, request.DestinationPath);

        // Synthesis is sequential: parallel requests would overload the GPU.
        var wavParts = new List<byte[]>(fragments.Count);
        for (var i = 0; i < fragments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new AudioExportProgress(AudioExportStage.Synthesizing, i, fragments.Count));

            var wav = await _apiClient.SynthesizeAsync(
                new TtsRequestDto
                {
                    Text = fragments[i].Text,
                    Voice = request.Voice,
                    Speed = request.Speed,
                    Volume = 1.0,
                    Translate = false
                },
                cancellationToken).ConfigureAwait(false);

            wavParts.Add(wav);
        }

        progress?.Report(new AudioExportProgress(AudioExportStage.Concatenating, fragments.Count, fragments.Count));
        var combined = WavAudioConcatenator.Concat(wavParts);

        var output = combined;
        if (request.Format == AudioExportFormat.Mp3)
        {
            progress?.Report(new AudioExportProgress(AudioExportStage.Converting, fragments.Count, fragments.Count));
            output = await _apiClient.ConvertAsync(combined, Mp3Format, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new AudioExportProgress(AudioExportStage.Writing, fragments.Count, fragments.Count));
        await WriteAtomicallyAsync(request.DestinationPath, output, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Експорт завершено: {Bytes} байт → {Path}", output.Length, request.DestinationPath);
    }

    /// <summary>
    /// Writes the file atomically: first to a temporary file in the same directory,
    /// then moves it over the destination.
    /// </summary>
    /// <param name="destinationPath">Destination path.</param>
    /// <param name="data">File contents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static async Task WriteAtomicallyAsync(
        string destinationPath,
        byte[] data,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(tempPath, data, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>Removes the temporary file if it was left behind after an error.</summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cleanup must not mask the original error.
        }
    }
}

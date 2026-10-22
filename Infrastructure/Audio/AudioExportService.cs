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
/// Експорт озвученого тексту у файл: послідовний синтез → склеювання WAV →
/// конвертація (MP4) → атомарний запис.
/// </summary>
public sealed class AudioExportService : IAudioExportService
{
    private const string Mp4Format = "mp4";

    private readonly ITtsApiClient _apiClient;
    private readonly ITextChunker _textChunker;
    private readonly ILogger<AudioExportService> _logger;

    /// <summary>
    /// Створює сервіс експорту.
    /// </summary>
    /// <param name="apiClient">Клієнт TTS-сервера.</param>
    /// <param name="textChunker">Сервіс поділу тексту на фрагменти.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
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

        // Синтез послідовний: паралельні запити перевантажили б GPU.
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
        if (request.Format == AudioExportFormat.Mp4)
        {
            progress?.Report(new AudioExportProgress(AudioExportStage.Converting, fragments.Count, fragments.Count));
            output = await _apiClient.ConvertAsync(combined, Mp4Format, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new AudioExportProgress(AudioExportStage.Writing, fragments.Count, fragments.Count));
        await WriteAtomicallyAsync(request.DestinationPath, output, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Експорт завершено: {Bytes} байт → {Path}", output.Length, request.DestinationPath);
    }

    /// <summary>
    /// Записує файл атомарно: спершу в тимчасовий файл у тому ж каталозі,
    /// потім переміщує його поверх цільового.
    /// </summary>
    /// <param name="destinationPath">Цільовий шлях.</param>
    /// <param name="data">Вміст файлу.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
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

    /// <summary>Прибирає тимчасовий файл, якщо він залишився після помилки.</summary>
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
            // Прибирання не повинно маскувати початкову помилку.
        }
    }
}

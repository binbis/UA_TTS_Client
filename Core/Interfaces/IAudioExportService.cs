#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Експорт озвученого тексту в аудіофайл (MP4 або WAV).
/// </summary>
/// <remarks>
/// Сервіс не залежить від UI: діалог вибору файлу викликає ViewModel, а сюди передається
/// готовий шлях. Синтез виконується послідовно, щоб не перевантажувати GPU.
/// </remarks>
public interface IAudioExportService
{
    /// <summary>
    /// Синтезує весь текст, склеює фрагменти й записує файл у вибраному форматі.
    /// </summary>
    /// <param name="request">Параметри експорту.</param>
    /// <param name="progress">Отримувач прогресу або <see langword="null"/>.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> дорівнює <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Текст порожній або не містить фрагментів.</exception>
    /// <exception cref="InvalidDataException">WAV-фрагменти мають неочікуваний формат.</exception>
    /// <exception cref="TtsApiException">Сервер недоступний або повернув помилку.</exception>
    /// <exception cref="IOException">Не вдалося записати файл.</exception>
    Task ExportAsync(
        AudioExportRequest request,
        IProgress<AudioExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

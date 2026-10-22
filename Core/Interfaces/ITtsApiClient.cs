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
/// Клієнт HTTP-API TTS-сервера.
/// </summary>
/// <remarks>
/// Усі методи перетворюють транспортні збої на <see cref="TtsApiException"/>,
/// тому викликати їх можна безпосередньо з ViewModel.
/// </remarks>
public interface ITtsApiClient
{
    /// <summary>
    /// Отримує список доступних голосів (<c>GET /api/voices</c>).
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Перелік імен голосів.</returns>
    /// <exception cref="TtsApiException">Сервер недоступний або повернув помилку.</exception>
    Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримує стан сервера (<c>GET /api/health</c>).
    /// </summary>
    /// <remarks>
    /// Якщо сервер старий і не має <c>/api/health</c> (404), метод автоматично перевіряє
    /// доступність через <c>GET /api/voices</c> і повертає синтетичну відповідь.
    /// </remarks>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Стан сервера.</returns>
    /// <exception cref="TtsApiException">Сервер недоступний або повернув помилку.</exception>
    Task<ServerHealth> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Вербалізує текст — перетворює числа та скорочення у слова (<c>POST /api/verbalize</c>).
    /// </summary>
    /// <param name="text">Вхідний текст.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Текст після вербалізації.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> порожній.</exception>
    /// <exception cref="TtsApiException">Сервер недоступний або повернув помилку.</exception>
    Task<string> VerbalizeAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Синтезує мовлення (<c>POST /api/synthesize</c>).
    /// </summary>
    /// <param name="request">Параметри синтезу.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Вміст WAV-файлу (24 кГц, моно, 16-bit PCM) у пам'яті.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> дорівнює <see langword="null"/>.</exception>
    /// <exception cref="TtsApiException">Сервер недоступний або повернув помилку.</exception>
    Task<byte[]> SynthesizeAsync(TtsRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Конвертує WAV у вказаний формат (<c>POST /api/convert</c>).
    /// </summary>
    /// <param name="wavData">Вміст WAV-файлу.</param>
    /// <param name="format">Цільовий формат, наприклад <c>mp4</c>.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Вміст конвертованого файлу.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wavData"/> дорівнює <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="format"/> порожній.</exception>
    /// <exception cref="TtsApiException">Сервер недоступний або повернув помилку.</exception>
    Task<byte[]> ConvertAsync(byte[] wavData, string format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Витягує текст із файлу документа (<c>POST /api/parse_file</c>).
    /// </summary>
    /// <param name="fileStream">Потік із вмістом файлу. Після завершення виклику потік звільняється.</param>
    /// <param name="fileName">Ім'я файлу разом із розширенням (<c>.pdf</c>, <c>.docx</c>, <c>.odt</c>, <c>.txt</c>).</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Розпізнаний текст.</returns>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    /// <exception cref="TtsApiException">Сервер недоступний або повернув помилку.</exception>
    Task<string> ParseFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}

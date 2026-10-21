#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Збереження та завантаження налаштувань застосунку.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Завантажує налаштування з диска.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>
    /// Збережений конфіг, або конфіг зі значеннями за замовчуванням, якщо файл відсутній чи пошкоджений.
    /// </returns>
    Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Атомарно зберігає налаштування на диск.
    /// </summary>
    /// <param name="config">Конфігурація для збереження.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> дорівнює <see langword="null"/>.</exception>
    /// <exception cref="IOException">Не вдалося записати файл.</exception>
    Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default);
}

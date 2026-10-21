#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Відтворення аудіо з пам'яті (без запису тимчасових файлів на диск).
/// </summary>
public interface IAudioPlayerService
{
    /// <summary>Поточний стан відтворення.</summary>
    PlaybackState State { get; }

    /// <summary>Поточна гучність у діапазоні від 0.0 до 1.0.</summary>
    double Volume { get; }

    /// <summary>Виникає щоразу, коли змінюється <see cref="State"/>.</summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// Завантажує WAV у пам'ять і починає відтворення.
    /// </summary>
    /// <param name="wavData">Повний вміст WAV-файлу.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <exception cref="ArgumentNullException"><paramref name="wavData"/> дорівнює <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="wavData"/> порожній.</exception>
    Task PlayAsync(byte[] wavData, CancellationToken cancellationToken = default);

    /// <summary>Призупиняє відтворення, зберігаючи позицію.</summary>
    void Pause();

    /// <summary>Продовжує відтворення після <see cref="Pause"/>.</summary>
    void Resume();

    /// <summary>Зупиняє відтворення та звільняє потік.</summary>
    void Stop();

    /// <summary>
    /// Встановлює гучність відтворення.
    /// </summary>
    /// <param name="volume">Значення від 0.0 до 1.0; значення поза межами обрізаються.</param>
    void SetVolume(double volume);
}

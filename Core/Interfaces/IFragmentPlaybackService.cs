#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Синтезує й відтворює фрагменти послідовно, пакетами по N.
/// </summary>
/// <remarks>
/// <para><see cref="StartAsync"/> повертається, щойно перший фрагмент почав грати,
/// а не після завершення всього пакета: інакше UI був би «зайнятий» хвилинами.</para>
/// <para>Між пакетами відтворення зупиняється; наступний пакет запускає
/// <see cref="ContinueAsync"/> (вручну).</para>
/// </remarks>
public interface IFragmentPlaybackService
{
    /// <summary>Виникає, коли черговий фрагмент починає відтворюватися.</summary>
    event EventHandler<FragmentStartedEventArgs>? FragmentStarted;

    /// <summary>Виникає після завершення пакета, якщо лишилися наступні.</summary>
    event EventHandler<BatchCompletedEventArgs>? BatchCompleted;

    /// <summary>Виникає після відтворення останнього фрагмента черги.</summary>
    event EventHandler? QueueCompleted;

    /// <summary>Виникає, якщо черга зупинилася через помилку після старту.</summary>
    event EventHandler<Exception>? Failed;

    /// <summary>Чи існує активна черга (грає, на паузі або очікує наступний пакет).</summary>
    bool HasQueue { get; }

    /// <summary>Чи завершився пакет і черга чекає на <see cref="ContinueAsync"/>.</summary>
    bool IsBatchPending { get; }

    /// <summary>Номер фрагмента, що грає зараз (з 1; 0 — якщо нічого не грає).</summary>
    int CurrentFragment { get; }

    /// <summary>Загальна кількість фрагментів у черзі.</summary>
    int TotalFragments { get; }

    /// <summary>Номер поточного пакета (з 1).</summary>
    int CurrentBatch { get; }

    /// <summary>Загальна кількість пакетів.</summary>
    int TotalBatches { get; }

    /// <summary>
    /// Запускає чергу: синтезує фрагменти послідовно, пакетами по
    /// <see cref="SpeechQueueOptions.BatchSize"/>, і починає відтворення.
    /// </summary>
    /// <param name="fragments">Фрагменти в порядку відтворення.</param>
    /// <param name="options">Голос, швидкість і розмір пакета.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Завершується, коли перший фрагмент почав грати, або з помилкою старту.</returns>
    /// <exception cref="ArgumentNullException">Аргумент дорівнює <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Список фрагментів порожній.</exception>
    /// <exception cref="UA_TTS_Client.Core.Exceptions.TtsApiException">Сервер не зміг синтезувати перший фрагмент.</exception>
    Task StartAsync(
        IReadOnlyList<TextFragment> fragments,
        SpeechQueueOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Запускає наступний пакет після <see cref="BatchCompleted"/>.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Завершується, коли перший фрагмент нового пакета почав грати.</returns>
    Task ContinueAsync(CancellationToken cancellationToken = default);

    /// <summary>Призупиняє поточний фрагмент і чергу.</summary>
    void Pause();

    /// <summary>Продовжує відтворення після <see cref="Pause"/>.</summary>
    void Resume();

    /// <summary>Зупиняє чергу, скасовує синтез і скидає лічильники.</summary>
    void Stop();
}

#nullable enable

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Параметри черги відтворення фрагментів.
/// </summary>
public sealed record SpeechQueueOptions
{
    /// <summary>Мінімальна кількість фрагментів у пакеті.</summary>
    public const int MinBatchSize = 1;

    /// <summary>Максимальна кількість фрагментів у пакеті.</summary>
    public const int MaxBatchSize = 10;

    /// <summary>Кількість фрагментів у пакеті за замовчуванням.</summary>
    public const int DefaultBatchSize = 5;

    /// <summary>Ім'я голосу або <see langword="null"/> для голосу за замовчуванням.</summary>
    public string? Voice { get; init; }

    /// <summary>Швидкість мовлення, що передається серверу.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Кількість фрагментів в одному пакеті (від 1 до 10).</summary>
    public int BatchSize { get; init; } = DefaultBatchSize;

    /// <summary>
    /// Чи зупиняти відтворення після кожного пакета (типово) або грати всі пакети підряд.
    /// </summary>
    public bool PauseAfterEachBatch { get; init; } = true;
}

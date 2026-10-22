#nullable enable

using System;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Дані події про завершення пакета фрагментів.
/// </summary>
/// <param name="completedBatch">Номер завершеного пакета (з 1).</param>
/// <param name="totalBatches">Загальна кількість пакетів.</param>
/// <param name="hasNext">Чи є наступний пакет для продовження.</param>
public sealed class BatchCompletedEventArgs(int completedBatch, int totalBatches, bool hasNext) : EventArgs
{
    /// <summary>Номер завершеного пакета (з 1).</summary>
    public int CompletedBatch { get; } = completedBatch;

    /// <summary>Загальна кількість пакетів.</summary>
    public int TotalBatches { get; } = totalBatches;

    /// <summary>Чи є наступний пакет для продовження.</summary>
    public bool HasNext { get; } = hasNext;
}

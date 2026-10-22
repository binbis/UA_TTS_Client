#nullable enable

using System;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Дані події про початок відтворення фрагмента.
/// </summary>
/// <param name="fragment">Фрагмент, який почав відтворюватися.</param>
/// <param name="batch">Номер поточного пакета (з 1).</param>
/// <param name="totalBatches">Загальна кількість пакетів.</param>
public sealed class FragmentStartedEventArgs(TextFragment fragment, int batch, int totalBatches) : EventArgs
{
    /// <summary>Фрагмент, який почав відтворюватися.</summary>
    public TextFragment Fragment { get; } = fragment ?? throw new ArgumentNullException(nameof(fragment));

    /// <summary>Номер поточного пакета (з 1).</summary>
    public int Batch { get; } = batch;

    /// <summary>Загальна кількість пакетів.</summary>
    public int TotalBatches { get; } = totalBatches;
}

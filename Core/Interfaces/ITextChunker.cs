#nullable enable

using System;
using System.Collections.Generic;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Поділ тексту на логічні фрагменти для порційного синтезу.
/// </summary>
/// <remarks>
/// Реалізація не повинна залежати від UI, мережі чи аудіо: це чиста синхронна функція
/// «текст → фрагменти».
/// </remarks>
public interface ITextChunker
{
    /// <summary>
    /// Розбиває текст на фрагменти згідно з режимом.
    /// </summary>
    /// <param name="text">Вхідний текст.</param>
    /// <param name="options">Параметри поділу.</param>
    /// <returns>Фрагменти з послідовними номерами, починаючи з 1; порожній список, якщо тексту немає.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> або <paramref name="options"/> дорівнює <see langword="null"/>.</exception>
    IReadOnlyList<TextFragment> Split(string text, TextChunkingOptions options);
}

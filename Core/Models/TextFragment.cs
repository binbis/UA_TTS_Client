#nullable enable

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Фрагмент тексту, який синтезується й відтворюється як окрема одиниця.
/// </summary>
/// <param name="Index">Порядковий номер фрагмента, починаючи з 1.</param>
/// <param name="Text">Текст фрагмента.</param>
public sealed record TextFragment(int Index, string Text);

#nullable enable

using System;
using System.Collections.Generic;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Splits text into logical fragments for chunked synthesis.
/// </summary>
/// <remarks>
/// The implementation must not depend on the UI, the network, or audio: it is a pure synchronous function
/// "text → fragments".
/// </remarks>
public interface ITextChunker
{
    /// <summary>
    /// Splits the text into fragments according to the mode.
    /// </summary>
    /// <param name="text">Input text.</param>
    /// <param name="options">Chunking options.</param>
    /// <returns>Fragments with sequential numbers starting from 1; an empty list if there is no text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    IReadOnlyList<TextFragment> Split(string text, TextChunkingOptions options);
}

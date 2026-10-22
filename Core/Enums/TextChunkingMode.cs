#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Спосіб поділу тексту на фрагменти перед синтезом.
/// </summary>
public enum TextChunkingMode
{
    /// <summary>Кожен абзац — окремий фрагмент; задовгі абзаци додатково ріжуться по реченнях.</summary>
    Paragraphs,

    /// <summary>Кожне речення — окремий фрагмент.</summary>
    Sentences,

    /// <summary>Речення пакуються у фрагменти до 250 символів, без обривання речень.</summary>
    MaxLength
}

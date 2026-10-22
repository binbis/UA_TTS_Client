#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Параметри поділу тексту на фрагменти.
/// </summary>
public sealed record TextChunkingOptions
{
    /// <summary>Максимальна довжина фрагмента за замовчуванням (символів).</summary>
    public const int DefaultMaxChunkLength = 250;

    /// <summary>Режим поділу.</summary>
    public TextChunkingMode Mode { get; init; } = TextChunkingMode.MaxLength;

    /// <summary>Максимальна довжина фрагмента в символах.</summary>
    public int MaxChunkLength { get; init; } = DefaultMaxChunkLength;
}

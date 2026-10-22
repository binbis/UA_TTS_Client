#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Поточний прогрес експорту аудіо.
/// </summary>
/// <param name="Stage">Етап експорту.</param>
/// <param name="CompletedFragments">Скільки фрагментів уже синтезовано.</param>
/// <param name="TotalFragments">Загальна кількість фрагментів.</param>
public sealed record AudioExportProgress(
    AudioExportStage Stage,
    int CompletedFragments,
    int TotalFragments);

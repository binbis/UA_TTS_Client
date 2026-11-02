#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Current audio export progress.
/// </summary>
/// <param name="Stage">Export stage.</param>
/// <param name="CompletedFragments">Number of fragments already synthesized.</param>
/// <param name="TotalFragments">Total number of fragments.</param>
public sealed record AudioExportProgress(
    AudioExportStage Stage,
    int CompletedFragments,
    int TotalFragments);

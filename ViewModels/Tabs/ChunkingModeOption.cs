#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Chunking mode option for the list in settings.
/// </summary>
/// <param name="Mode">Text splitting mode.</param>
/// <param name="Title">User-facing label.</param>
public sealed record ChunkingModeOption(TextChunkingMode Mode, string Title);

#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Варіант режиму фрагментації для списку в налаштуваннях.
/// </summary>
/// <param name="Mode">Режим поділу тексту.</param>
/// <param name="Title">Підпис для користувача.</param>
public sealed record ChunkingModeOption(TextChunkingMode Mode, string Title);

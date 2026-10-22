#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Варіант формату збереження аудіо для списку в налаштуваннях.
/// </summary>
/// <param name="Format">Формат файлу.</param>
/// <param name="Title">Підпис для користувача.</param>
public sealed record ExportFormatOption(AudioExportFormat Format, string Title);

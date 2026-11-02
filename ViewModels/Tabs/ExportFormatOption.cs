#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Audio save format option for the list in settings.
/// </summary>
/// <param name="Format">File format.</param>
/// <param name="Title">User-facing label.</param>
public sealed record ExportFormatOption(AudioExportFormat Format, string Title);

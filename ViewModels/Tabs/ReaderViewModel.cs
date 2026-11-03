#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// "Reader" tab — text recognition from files (PDF, DOCX, ODT, TXT).
/// </summary>
/// <remarks>Placeholder: the functionality is not implemented yet. See Doc/Roadmap.md.</remarks>
public partial class ReaderViewModel : ViewModelBase
{
    /// <summary>Tab title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Reader";

    /// <summary>Description of the upcoming functionality.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } =
        "Тут з'явиться завантаження PDF, DOCX, ODT і TXT та озвучення розпізнаного тексту.";
}

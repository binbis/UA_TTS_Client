#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// "Character Voice" tab — speech for character lines using different voices.
/// </summary>
/// <remarks>Placeholder: the functionality is not implemented yet. See Doc/Roadmap.md.</remarks>
public partial class CharacterVoiceViewModel : ViewModelBase
{
    /// <summary>Tab title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Character Voice";

    /// <summary>Description of the upcoming functionality.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } =
        "Тут з'явиться розподіл реплік за персонажами та озвучення кожної репліки окремим голосом.";
}

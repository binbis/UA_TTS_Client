#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Вкладка «Character Voice» — озвучення реплік персонажів різними голосами.
/// </summary>
/// <remarks>Заглушка: функціонал з'явиться в наступній ітерації.</remarks>
public partial class CharacterVoiceViewModel : ViewModelBase
{
    /// <summary>Заголовок вкладки.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Character Voice";

    /// <summary>Опис майбутньої функціональності.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } =
        "Тут з'явиться розподіл реплік за персонажами та озвучення кожної репліки окремим голосом.";
}

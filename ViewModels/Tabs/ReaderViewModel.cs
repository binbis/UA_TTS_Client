#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Вкладка «Reader» — розпізнавання тексту з файлів (PDF, DOCX, ODT, TXT).
/// </summary>
/// <remarks>Заглушка: функціонал з'явиться в наступній ітерації.</remarks>
public partial class ReaderViewModel : ViewModelBase
{
    /// <summary>Заголовок вкладки.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Reader";

    /// <summary>Опис майбутньої функціональності.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } =
        "Тут з'явиться завантаження PDF, DOCX, ODT і TXT та озвучення розпізнаного тексту.";
}

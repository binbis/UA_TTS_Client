using Avalonia.Controls;
using UA_TTS_Client.ViewModels.Tabs;

namespace UA_TTS_Client.Views.Tabs;

public partial class CharacterVoiceView : UserControl
{
    public CharacterVoiceView()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            DataContext = new CharacterVoiceViewModel();
        }
    }
}

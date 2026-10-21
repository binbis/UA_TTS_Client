using Avalonia.Controls;
using DesignData = UA_TTS_Client.ViewModels.Design.DesignData;

namespace UA_TTS_Client.Views.Tabs;

public partial class QuickTtsView : UserControl
{
    public QuickTtsView()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            DataContext = DesignData.QuickTts;
        }
    }
}

using Avalonia.Controls;
using DesignData = UA_TTS_Client.ViewModels.Design.DesignData;

namespace UA_TTS_Client.Views.Tabs;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            DataContext = DesignData.Settings;
        }
    }
}

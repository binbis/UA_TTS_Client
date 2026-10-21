using Avalonia.Controls;
using DesignData = UA_TTS_Client.ViewModels.Design.DesignData;

namespace UA_TTS_Client.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            DataContext = DesignData.MainWindow;
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace UA_TTS_Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
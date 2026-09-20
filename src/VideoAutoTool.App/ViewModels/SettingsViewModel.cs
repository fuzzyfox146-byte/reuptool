using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoTool.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _ffmpegPath = "";

    [ObservableProperty]
    private int _parallelCount = 1;
}

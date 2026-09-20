using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoTool.Core.Ffmpeg;

namespace VideoAutoTool.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(FfmpegPaths paths)
    {
        FfmpegPath = paths.FfmpegPath;
        FfmpegVersion = string.IsNullOrWhiteSpace(paths.Version) ? "(không đọc được phiên bản)" : paths.Version;
    }

    [ObservableProperty]
    private string _ffmpegPath = "";

    [ObservableProperty]
    private string _ffmpegVersion = "";

    [ObservableProperty]
    private int _parallelCount = 2;
}

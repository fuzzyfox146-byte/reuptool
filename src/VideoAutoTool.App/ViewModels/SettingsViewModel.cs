using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Render;

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

    [ObservableProperty]
    private int _queueBatchSize = 9;

    [ObservableProperty]
    private int _backgroundScalePercent = TemplateRenderOptions.DefaultBackgroundScalePercent;

    public IReadOnlyList<int> ParallelOptions { get; } = [1, 2, 3];

    partial void OnQueueBatchSizeChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 99);
        if (clamped != value)
        {
            QueueBatchSize = clamped;
        }
    }

    partial void OnParallelCountChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 3);
        if (clamped != value)
        {
            ParallelCount = clamped;
        }
    }

    partial void OnBackgroundScalePercentChanged(int value)
    {
        var clamped = TemplateRenderOptions.ClampScalePercent(value);
        if (clamped != value)
        {
            BackgroundScalePercent = clamped;
        }
    }
}

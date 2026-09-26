using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.App.Services;

namespace VideoAutoTool.App.ViewModels;

public partial class DownloadChannelRowViewModel : ObservableObject
{
    private readonly Action<DownloadChannelRowViewModel> _browse;
    private readonly Action<DownloadChannelRowViewModel> _remove;
    private readonly Action<DownloadChannelRowViewModel> _start;
    private readonly Action<DownloadChannelRowViewModel> _stop;
    private CancellationTokenSource? _run;

    public DownloadChannelRowViewModel(
        Action<DownloadChannelRowViewModel> browse,
        Action<DownloadChannelRowViewModel> remove,
        Action<DownloadChannelRowViewModel> start,
        Action<DownloadChannelRowViewModel> stop)
    {
        _browse = browse;
        _remove = remove;
        _start = start;
        _stop = stop;
    }

    [ObservableProperty]
    private string _parentFolder = "";

    [ObservableProperty]
    private string _channelUrl = "";

    [ObservableProperty]
    private int _playlistStart = 1;

    [ObservableProperty]
    private int _playlistEnd = 20;

    [ObservableProperty]
    private int _nameStart = 1;

    [ObservableProperty]
    private string _language = "en";

    [ObservableProperty]
    private string _status = "Chờ";

    [ObservableProperty]
    private string _videoProgress = "Video —";

    [ObservableProperty]
    private string _textProgress = "Phụ đề —";

    private readonly HashSet<string> _videoSeen = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _textSeen = new(StringComparer.OrdinalIgnoreCase);

    private int _videoCount;

    private int _textCount;

    [ObservableProperty]
    private bool _isBusy;

    public void ResetProgress()
    {
        _videoSeen.Clear();
        _textSeen.Clear();
        _videoCount = 0;
        _textCount = 0;
        VideoProgress = "Video —";
        TextProgress = "Phụ đề —";
    }

    public void NoteDownload(string kind, string number)
    {
        var total = PlaylistEnd >= PlaylistStart ? PlaylistEnd - PlaylistStart + 1 : 0;
        if (string.Equals(kind, "Text", StringComparison.Ordinal))
        {
            if (_textSeen.Add(number))
            {
                _textCount++;
            }

            TextProgress = total > 0 ? $"Phụ đề {_textCount}/{total} · {number}" : $"Phụ đề {number}";
            return;
        }

        if (_videoSeen.Add(number))
        {
            _videoCount++;
        }

        VideoProgress = total > 0 ? $"Video {_videoCount}/{total} · {number}" : $"Video {number}";
    }

    public bool CanEdit => !IsBusy;

    public string StartLabel =>
        Status is "Đã dừng" or "Lỗi" or "Cookie lỗi" ? "Tải tiếp" : "Tải";

    public void AttachRun(CancellationTokenSource run) => _run = run;

    public void CancelRun() => _run?.Cancel();

    public bool IsBlank =>
        string.IsNullOrWhiteSpace(ParentFolder) && string.IsNullOrWhiteSpace(ChannelUrl);

    public DownloadChannelSession ToSession() => new()
    {
        ParentFolder = ParentFolder,
        ChannelUrl = ChannelUrl,
        PlaylistStart = PlaylistStart,
        PlaylistEnd = PlaylistEnd,
        NameStart = NameStart,
        Language = Language
    };

    [RelayCommand(CanExecute = nameof(CanEditRow))]
    private void Browse() => _browse(this);

    [RelayCommand(CanExecute = nameof(CanEditRow))]
    private void Remove() => _remove(this);

    [RelayCommand(CanExecute = nameof(CanEditRow))]
    private void Start() => _start(this);

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _stop(this);

    private bool CanEditRow() => !IsBusy;

    private bool CanStop() => IsBusy;

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(StartLabel));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(StartLabel));
        BrowseCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }
}

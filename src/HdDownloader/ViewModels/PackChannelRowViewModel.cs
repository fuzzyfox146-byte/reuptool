using CommunityToolkit.Mvvm.ComponentModel;
using HdDownloader.Resources;

namespace HdDownloader.ViewModels;

public partial class PackChannelRowViewModel : ObservableObject
{
    private readonly HashSet<string> _videoSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _thumbSeen = new(StringComparer.OrdinalIgnoreCase);
    private int _videoCount;
    private int _thumbCount;
    private string _root = "";
    private bool _downloadSubtitles;

    [ObservableProperty]
    private string _folderName = "HD036";

    [ObservableProperty]
    private string _channelUrl = "";

    [ObservableProperty]
    private int _playlistStart = 1;

    [ObservableProperty]
    private int _playlistEnd = 10;

    [ObservableProperty]
    private int _nameStart = 1;

    [ObservableProperty]
    private string _status = UiText.Waiting;

    [ObservableProperty]
    private string _videoProgress = UiText.VideoIdle;

    [ObservableProperty]
    private string _thumbProgress = UiText.ThumbIdle;

    [ObservableProperty]
    private bool _isBusy;

    public bool CanEdit => !IsBusy;

    public bool IsBlank =>
        string.IsNullOrWhiteSpace(FolderName) && string.IsNullOrWhiteSpace(ChannelUrl);

    public string LayoutText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_root) || string.IsNullOrWhiteSpace(FolderName))
            {
                return "";
            }

            var folder = Path.Combine(_root.Trim(), FolderName.Trim());
            var sub = _downloadSubtitles ? @"    ·    " + folder + @"\sub" : "";
            return folder + @"\video    ·    " + folder + @"\thumb" + sub;
        }
    }

    public void SetRoot(string root)
    {
        _root = root ?? "";
        OnPropertyChanged(nameof(LayoutText));
    }

    public void SetDownloadSubtitles(bool enabled)
    {
        _downloadSubtitles = enabled;
        OnPropertyChanged(nameof(LayoutText));
    }

    public void ResetProgress()
    {
        _videoSeen.Clear();
        _thumbSeen.Clear();
        _videoCount = 0;
        _thumbCount = 0;
        VideoProgress = UiText.VideoIdle;
        ThumbProgress = UiText.ThumbIdle;
    }

    public void NoteDownload(string kind, string number)
    {
        var total = PlaylistEnd >= PlaylistStart ? PlaylistEnd - PlaylistStart + 1 : 0;
        if (string.Equals(kind, "Thumb", StringComparison.Ordinal))
        {
            if (_thumbSeen.Add(number))
            {
                _thumbCount++;
            }

            ThumbProgress = UiText.ThumbCount(_thumbCount, total, number);
            return;
        }

        if (_videoSeen.Add(number))
        {
            _videoCount++;
        }

        VideoProgress = UiText.VideoCount(_videoCount, total, number);
    }

    partial void OnFolderNameChanged(string value) => OnPropertyChanged(nameof(LayoutText));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
}

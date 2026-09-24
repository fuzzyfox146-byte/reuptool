using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.App.Services;
using VideoAutoTool.Core.Download;

namespace VideoAutoTool.App.ViewModels;

public partial class DownloadViewModel : ObservableObject
{
    private readonly IUiDialogs _dialogs;
    private readonly SettingsViewModel _settings;
    private readonly SourceDownloader _downloader;
    private readonly object _slotLock = new();
    private int _activeSlots;
    private bool _restoring;

    public DownloadViewModel(IUiDialogs dialogs, SettingsViewModel settings, SourceDownloader downloader)
    {
        _dialogs = dialogs;
        _settings = settings;
        _downloader = downloader;
        Channels.CollectionChanged += (_, _) => NotifyWorkspace();
        YtDlpPath = DownloadToolLocator.FindYtDlp() ?? "";
        CookiesPath = DownloadToolLocator.FindCookies(YtDlpPath) ?? "";
        Channels.Add(CreateRow());
    }

    public event EventHandler? WorkspaceChanged;

    public ObservableCollection<DownloadChannelRowViewModel> Channels { get; } = [];

    public ObservableCollection<string> LogLines { get; } = [];

    public IReadOnlyList<int> ParallelOptions { get; } = [1, 2, 3, 4];

    public IReadOnlyList<QualityOption> QualityOptions { get; } =
    [
        new("144", "144p"),
        new("360", "360p"),
        new("480", "480p"),
        new("720", "720p"),
        new("1080", "1080p")
    ];

    private readonly Dictionary<string, int> _logSlots = [];

    [ObservableProperty]
    private string _ytDlpPath = "";

    [ObservableProperty]
    private string _cookiesPath = "";

    [ObservableProperty]
    private int _downloadParallel = 2;

    [ObservableProperty]
    private string _videoQuality = "144";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _status = "Chọn cookie dùng chung, rồi thêm từng kênh.";

    [RelayCommand]
    private void BrowseYtDlp()
    {
        var path = _dialogs.PickFile("Chọn yt-dlp.exe", "yt-dlp (*.exe)|*.exe|Tất cả (*.*)|*.*", DirectoryOf(YtDlpPath));
        if (!string.IsNullOrWhiteSpace(path))
        {
            YtDlpPath = path;
        }
    }

    [RelayCommand]
    private void BrowseCookies()
    {
        var path = _dialogs.PickFile(
            "Chọn cookies.txt dùng chung",
            "Cookie (*.txt)|*.txt|Tất cả (*.*)|*.*",
            DirectoryOf(CookiesPath));
        if (!string.IsNullOrWhiteSpace(path))
        {
            CookiesPath = path;
            Status = "Cookie dùng chung: " + path;
        }
    }

    [RelayCommand]
    private void AddChannel() => Channels.Add(CreateRow());

    [RelayCommand]
    private void Start()
    {
        foreach (var row in Channels.Where(row => !row.IsBusy && !row.IsBlank).ToList())
        {
            StartRow(row);
        }
    }

    public void Restore(
        string ytDlpPath,
        string cookiesPath,
        int parallel,
        string videoQuality,
        IReadOnlyList<DownloadChannelSession> channels)
    {
        _restoring = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(ytDlpPath))
            {
                YtDlpPath = ytDlpPath;
            }

            if (!string.IsNullOrWhiteSpace(cookiesPath))
            {
                CookiesPath = cookiesPath;
            }

            DownloadParallel = Math.Clamp(parallel, 1, 4);
            VideoQuality = string.IsNullOrWhiteSpace(videoQuality) ? "144" : videoQuality;
            Channels.Clear();
            foreach (var channel in channels)
            {
                var row = CreateRow();
                row.ParentFolder = channel.ParentFolder;
                row.ChannelUrl = channel.ChannelUrl;
                row.PlaylistStart = channel.PlaylistStart < 1 ? 1 : channel.PlaylistStart;
                row.PlaylistEnd = channel.PlaylistEnd < row.PlaylistStart ? row.PlaylistStart : channel.PlaylistEnd;
                row.NameStart = channel.NameStart < 1 ? 1 : channel.NameStart;
                row.Language = string.IsNullOrWhiteSpace(channel.Language) ? "en" : channel.Language;
                Channels.Add(row);
            }

            if (Channels.Count == 0)
            {
                Channels.Add(CreateRow());
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    public List<DownloadChannelSession> CaptureChannels() =>
        Channels.Select(row => row.ToSession()).ToList();

    public void CancelDownloads()
    {
        foreach (var row in Channels)
        {
            row.CancelRun();
        }
    }

    partial void OnYtDlpPathChanged(string value) => NotifyWorkspace();

    partial void OnCookiesPathChanged(string value) => NotifyWorkspace();

    partial void OnDownloadParallelChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 4);
        if (clamped != value)
        {
            DownloadParallel = clamped;
            return;
        }

        NotifyWorkspace();
    }

    partial void OnVideoQualityChanged(string value)
    {
        if (value is not ("144" or "360" or "480" or "720" or "1080"))
        {
            VideoQuality = "144";
            return;
        }

        NotifyWorkspace();
    }

    private void NotifyWorkspace()
    {
        if (!_restoring)
        {
            WorkspaceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private DownloadChannelRowViewModel CreateRow()
    {
        var row = new DownloadChannelRowViewModel(BrowseFolder, ClearArchive, RemoveRow, StartRow, StopRow);
        row.PropertyChanged += (_, _) => NotifyWorkspace();
        return row;
    }

    private void BrowseFolder(DownloadChannelRowViewModel row)
    {
        var path = _dialogs.PickFolder("Folder cha (sẽ tạo source và text)", row.ParentFolder);
        if (!string.IsNullOrWhiteSpace(path))
        {
            row.ParentFolder = path;
        }
    }

    private void ClearArchive(DownloadChannelRowViewModel row)
    {
        if (string.IsNullOrWhiteSpace(row.ParentFolder))
        {
            row.Status = "Chưa chọn folder.";
            return;
        }

        var removed = DownloadArchive.Clear(row.ParentFolder.Trim());
        row.Status = removed.Count == 0
            ? "Không có file lịch sử tải."
            : "Đã xóa lịch sử tải. Lần sau sẽ tải lại từ đầu.";
        ShowNotice(new DownloadNotice(null, row.ParentFolder + ": " + row.Status));
    }

    private void RemoveRow(DownloadChannelRowViewModel row)
    {
        if (row.IsBusy)
        {
            return;
        }

        Channels.Remove(row);
    }

    private void StartRow(DownloadChannelRowViewModel row)
    {
        if (row.IsBusy || row.IsBlank)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(YtDlpPath) || !File.Exists(YtDlpPath))
        {
            Status = "Không thấy yt-dlp. Hãy chọn file yt-dlp.exe.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CookiesPath) || !File.Exists(CookiesPath))
        {
            Status = "Không thấy file cookie. Hãy chọn cookies.txt dùng chung.";
            return;
        }

        var language = string.IsNullOrWhiteSpace(row.Language) ? "en" : row.Language.Trim();
        var request = new ChannelDownloadRequest(
            row.ParentFolder.Trim(),
            row.ChannelUrl.Trim(),
            row.PlaylistStart,
            row.PlaylistEnd,
            row.NameStart,
            language,
            VideoQuality);
        var tools = new DownloadTools(YtDlpPath, FfmpegLocation(), CookiesPath);
        var run = new CancellationTokenSource();
        var progress = new Progress<DownloadNotice>(ShowNotice);
        row.AttachRun(run);
        row.ResetProgress();
        row.Status = "Đang tải...";
        row.IsBusy = true;
        RefreshRunning();
        _ = RunRowAsync(row, tools, request, progress, run);
    }

    private void StopRow(DownloadChannelRowViewModel row) => row.CancelRun();

    private async Task RunRowAsync(
        DownloadChannelRowViewModel row,
        DownloadTools tools,
        ChannelDownloadRequest request,
        IProgress<DownloadNotice> progress,
        CancellationTokenSource run)
    {
        var acquired = false;
        try
        {
            await WaitSlotAsync(run.Token);
            acquired = true;
            var result = await _downloader.DownloadAsync(tools, request, progress, run.Token);
            row.Status = StatusText(result);
            if (result.Stop is DownloadStop.Failed or DownloadStop.CookieDead)
            {
                ShowNotice(new DownloadNotice(null, result.Detail));
            }
        }
        catch (OperationCanceledException)
        {
            row.Status = "Đã dừng";
        }
        catch (Exception ex)
        {
            row.Status = "Lỗi";
            ShowNotice(new DownloadNotice(null, ex.Message));
        }
        finally
        {
            if (acquired)
            {
                ReleaseSlot();
            }

            row.IsBusy = false;
            run.Dispose();
            RefreshRunning();
        }
    }

    private async Task WaitSlotAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_slotLock)
            {
                if (_activeSlots < Math.Clamp(DownloadParallel, 1, 4))
                {
                    _activeSlots++;
                    return;
                }
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private void ReleaseSlot()
    {
        lock (_slotLock)
        {
            if (_activeSlots > 0)
            {
                _activeSlots--;
            }
        }
    }

    private void RefreshRunning()
    {
        var running = Channels.Count(row => row.IsBusy);
        IsRunning = running > 0;
        Status = running > 0
            ? $"Đang tải {running} kênh. Dừng một kênh không dừng kênh khác."
            : "Có thể tải tiếp kênh đã dừng. File đã có sẽ không tải lại.";
    }

    private string FfmpegLocation()
    {
        var path = _settings.FfmpegPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        return File.Exists(path) ? Path.GetDirectoryName(path) ?? path : path;
    }

    private void ShowNotice(DownloadNotice notice)
    {
        if (!string.IsNullOrWhiteSpace(notice.Folder)
            && !string.IsNullOrWhiteSpace(notice.Kind)
            && !string.IsNullOrWhiteSpace(notice.FileNumber))
        {
            var folder = notice.Folder.Trim().TrimEnd('\\', '/');
            var row = Channels.FirstOrDefault(channel =>
                string.Equals(channel.ParentFolder.Trim().TrimEnd('\\', '/'), folder, StringComparison.OrdinalIgnoreCase));
            row?.NoteDownload(notice.Kind, notice.FileNumber);
        }

        if (string.IsNullOrWhiteSpace(notice.Text))
        {
            return;
        }

        if (!string.IsNullOrEmpty(notice.ReplaceKey)
            && _logSlots.TryGetValue(notice.ReplaceKey, out var index)
            && index >= 0
            && index < LogLines.Count)
        {
            LogLines[index] = notice.Text;
            return;
        }

        LogLines.Add(notice.Text);
        if (!string.IsNullOrEmpty(notice.ReplaceKey))
        {
            _logSlots[notice.ReplaceKey] = LogLines.Count - 1;
        }

        while (LogLines.Count > 20)
        {
            LogLines.RemoveAt(0);
            var stale = new List<string>();
            foreach (var pair in _logSlots.ToList())
            {
                if (pair.Value == 0)
                {
                    stale.Add(pair.Key);
                }
                else
                {
                    _logSlots[pair.Key] = pair.Value - 1;
                }
            }

            foreach (var key in stale)
            {
                _logSlots.Remove(key);
            }
        }
    }

    private static string StatusText(ChannelDownloadResult result) => result.Stop switch
    {
        DownloadStop.Completed => "Xong",
        DownloadStop.CookieDead => "Cookie lỗi",
        DownloadStop.StoppedBecauseCookie => "Dừng vì cookie",
        DownloadStop.Cancelled => "Đã dừng",
        _ => "Lỗi"
    };

    private static string? DirectoryOf(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        return Directory.Exists(directory) ? directory : null;
    }
}

public sealed record QualityOption(string Id, string Label);

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HdDownloader.Resources;
using HdDownloader.Services;
using Microsoft.Win32;
using VideoAutoTool.Core.Download;

namespace HdDownloader.ViewModels;

public partial class PackDownloadViewModel : ObservableObject
{
    private readonly PackDownloader _downloader;
    private CancellationTokenSource? _run;
    private bool _restoring;

    public PackDownloadViewModel(PackDownloader downloader)
    {
        _downloader = downloader;
        QualityOptions =
        [
            new PackQualityOption("144", "144p"),
            new PackQualityOption("360", "360p"),
            new PackQualityOption("480", "480p"),
            new PackQualityOption("720", "720p"),
            new PackQualityOption("1080", "1080p")
        ];
        ParallelOptions = [1, 2, 3, 4];
        SubtitleLanguageOptions =
        [
            new PackQualityOption("en,ja,pt", UiText.SubLangEnJaPt),
            new PackQualityOption("en", UiText.SubLangEn),
            new PackQualityOption("ja", UiText.SubLangJa),
            new PackQualityOption("pt", UiText.SubLangPt),
            new PackQualityOption("en,ja", UiText.SubLangEnJa),
            new PackQualityOption("ja,pt", UiText.SubLangJaPt),
            new PackQualityOption("en,pt", UiText.SubLangEnPt),
            new PackQualityOption("ko", UiText.SubLangKo),
            new PackQualityOption("zh", UiText.SubLangZh),
            new PackQualityOption("es", UiText.SubLangEs),
            new PackQualityOption("fr", UiText.SubLangFr),
            new PackQualityOption("de", UiText.SubLangDe),
            new PackQualityOption("vi", UiText.SubLangVi),
            new PackQualityOption("th", UiText.SubLangTh),
            new PackQualityOption("all", UiText.SubLangAll)
        ];
        Load();
    }

    public ObservableCollection<PackChannelRowViewModel> Channels { get; } = [];

    public ObservableCollection<string> LogLines { get; } = [];

    public IReadOnlyList<PackQualityOption> QualityOptions { get; }

    public IReadOnlyList<int> ParallelOptions { get; }

    public IReadOnlyList<PackQualityOption> SubtitleLanguageOptions { get; }

    [ObservableProperty]
    private string _ytDlpPath = "";

    [ObservableProperty]
    private string _cookiesPath = "";

    [ObservableProperty]
    private string _rootFolder = "";

    [ObservableProperty]
    private string _quality = "720";

    [ObservableProperty]
    private int _parallel = 3;

    [ObservableProperty]
    private bool _orderByViews = PackSettingsStore.DefaultOrderByViews;

    [ObservableProperty]
    private bool _downloadSubtitles = PackSettingsStore.DefaultDownloadSubtitles;

    [ObservableProperty]
    private string _subtitleLanguages = "en,ja,pt";

    public Visibility ViewOrderVisibility { get; } =
#if HD_VIEW_EDITION
        Visibility.Visible;
#else
        Visibility.Collapsed;
#endif

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _status = UiText.Idle;

    public void Save()
    {
        try
        {
            PackSettingsStore.Save(Capture());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = ex.Message;
        }
    }

    public void CancelAll() => _run?.Cancel();

    [RelayCommand]
    private void BrowseYtDlp()
    {
        var path = PickFile(UiText.PickYtDlp, UiText.FileFilterExe, DirectoryOf(YtDlpPath));
        if (!string.IsNullOrWhiteSpace(path))
        {
            YtDlpPath = path;
            if (string.IsNullOrWhiteSpace(CookiesPath))
            {
                CookiesPath = PackToolLocator.FindCookies(path) ?? "";
            }
        }
    }

    [RelayCommand]
    private void BrowseCookies()
    {
        var path = PickFile(UiText.PickCookies, UiText.FileFilterCookie, DirectoryOf(CookiesPath));
        if (!string.IsNullOrWhiteSpace(path))
        {
            CookiesPath = path;
        }
    }

    [RelayCommand]
    private void BrowseRoot()
    {
        var path = PickFolder(UiText.PickRoot, RootFolder);
        if (!string.IsNullOrWhiteSpace(path))
        {
            RootFolder = path;
        }
    }

    [RelayCommand]
    private void BrowseChannel(PackChannelRowViewModel row)
    {
        var initial = string.IsNullOrWhiteSpace(RootFolder) ? null : Path.Combine(RootFolder, row.FolderName);
        var path = PickFolder(UiText.PickChannel, initial);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            RootFolder = parent;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            row.FolderName = name;
        }
    }

    [RelayCommand]
    private void AddChannel()
    {
        var row = CreateRow();
        var previous = Channels.LastOrDefault()?.FolderName;
        row.FolderName = string.IsNullOrWhiteSpace(previous) ? "HD036" : NextFolderName(previous);
        Channels.Add(row);
        Save();
    }

    [RelayCommand]
    private void RemoveChannel(PackChannelRowViewModel row)
    {
        if (row.IsBusy)
        {
            return;
        }

        Channels.Remove(row);
        if (Channels.Count == 0)
        {
            Channels.Add(CreateRow());
        }

        Save();
    }

    [RelayCommand]
    private Task StartAllAsync() => StartAsync(Channels.Where(row => !row.IsBlank).ToList());

    [RelayCommand]
    private Task StartChannelAsync(PackChannelRowViewModel row) =>
        StartAsync(row.IsBlank ? [] : [row]);

    [RelayCommand]
    private void StopAll() => CancelAll();

    partial void OnYtDlpPathChanged(string value) => Persist();

    partial void OnCookiesPathChanged(string value) => Persist();

    partial void OnRootFolderChanged(string value)
    {
        foreach (var row in Channels)
        {
            row.SetRoot(value);
        }

        Persist();
    }

    partial void OnQualityChanged(string value)
    {
        if (value is not ("144" or "360" or "480" or "720" or "1080"))
        {
            Quality = "720";
            return;
        }

        Persist();
    }

    partial void OnParallelChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, PackDownloader.MaxParallel);
        if (clamped != value)
        {
            Parallel = clamped;
            return;
        }

        Persist();
    }

    public bool CanStart => !IsRunning;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanStart));

    private async Task StartAsync(IReadOnlyList<PackChannelRowViewModel> rows)
    {
        if (IsRunning)
        {
            Status = UiText.AlreadyRunning;
            return;
        }

        if (rows.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(YtDlpPath) || !File.Exists(YtDlpPath))
        {
            Status = UiText.MissingYtDlp;
            return;
        }

        if (string.IsNullOrWhiteSpace(CookiesPath) || !File.Exists(CookiesPath))
        {
            Status = UiText.MissingCookies;
            return;
        }

        var ffmpeg = PackToolLocator.FindFfmpegDirectory(YtDlpPath);
        if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(Path.Combine(ffmpeg, "ffmpeg.exe")))
        {
            Status = UiText.MissingFfmpeg;
            return;
        }

        var requests = new List<PackChannelRequest>();
        var selected = new List<PackChannelRowViewModel>();
        foreach (var row in rows)
        {
            var request = new PackChannelRequest(
                RootFolder.Trim(),
                row.FolderName.Trim(),
                row.ChannelUrl.Trim(),
                row.PlaylistStart,
                row.PlaylistEnd,
                row.NameStart,
                Quality,
                OrderByViews,
                DownloadSubtitles,
                SubtitleLanguages);
            requests.Add(request);
            selected.Add(row);
            row.ResetProgress();
            row.Status = UiText.Running;
            row.IsBusy = true;
        }

        var tools = new DownloadTools(YtDlpPath, ffmpeg, CookiesPath);
        var run = new CancellationTokenSource();
        _run = run;
        IsRunning = true;
        Status = UiText.Busy;
        var progress = new Progress<DownloadNotice>(notice => ShowNotice(notice));
        try
        {
            var results = await _downloader.DownloadAllAsync(tools, requests, Parallel, progress, run.Token);
            for (var i = 0; i < selected.Count; i++)
            {
                selected[i].Status = results[i].Detail;
                if (results[i].Stop is DownloadStop.Failed or DownloadStop.CookieDead)
                {
                    ShowNotice(new DownloadNotice(null, selected[i].FolderName + "  " + results[i].Detail));
                }
            }

            Status = UiText.Ready;
        }
        catch (OperationCanceledException)
        {
            foreach (var row in selected)
            {
                row.Status = UiText.Stopped;
            }

            Status = UiText.Stopped;
        }
        catch (Exception ex)
        {
            foreach (var row in selected)
            {
                row.Status = UiText.Error;
            }

            Status = ex.Message;
            ShowNotice(new DownloadNotice(null, ex.Message));
        }
        finally
        {
            foreach (var row in selected)
            {
                row.IsBusy = false;
            }

            IsRunning = false;
            if (ReferenceEquals(_run, run))
            {
                _run = null;
            }

            run.Dispose();
            Save();
        }
    }

    private void ShowNotice(DownloadNotice notice)
    {
        if (!string.IsNullOrWhiteSpace(notice.Text))
        {
            LogLines.Insert(0, notice.Text);
            while (LogLines.Count > 30)
            {
                LogLines.RemoveAt(LogLines.Count - 1);
            }
        }

        if (string.IsNullOrWhiteSpace(notice.Folder) || string.IsNullOrWhiteSpace(notice.FileNumber))
        {
            return;
        }

        var row = Channels.FirstOrDefault(channel =>
            string.Equals(PackFolders.Channel(ToRequest(channel)), notice.Folder, StringComparison.OrdinalIgnoreCase));
        if (row is null || string.IsNullOrWhiteSpace(notice.Kind))
        {
            return;
        }

        row.NoteDownload(notice.Kind, notice.FileNumber);
    }

    private PackChannelRequest ToRequest(PackChannelRowViewModel row) =>
        new(RootFolder, row.FolderName, row.ChannelUrl, row.PlaylistStart, row.PlaylistEnd, row.NameStart, Quality, OrderByViews, DownloadSubtitles, SubtitleLanguages);

    private void Load()
    {
        _restoring = true;
        try
        {
            var saved = PackSettingsStore.Load();
            YtDlpPath = string.IsNullOrWhiteSpace(saved.YtDlpPath) ? PackToolLocator.FindYtDlp() ?? "" : saved.YtDlpPath;
            CookiesPath = string.IsNullOrWhiteSpace(saved.CookiesPath) ? PackToolLocator.FindCookies(YtDlpPath) ?? "" : saved.CookiesPath;
            RootFolder = string.IsNullOrWhiteSpace(saved.RootFolder)
                ? (Directory.Exists(@"D:\") ? @"D:\" : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                : saved.RootFolder;
            Quality = saved.Quality is "144" or "360" or "480" or "720" or "1080" ? saved.Quality : "720";
            Parallel = Math.Clamp(saved.Parallel == 0 ? 3 : saved.Parallel, 1, PackDownloader.MaxParallel);
            OrderByViews = saved.OrderByViews;
            DownloadSubtitles = saved.DownloadSubtitles;
            SubtitleLanguages = SubtitleLanguageOptions.Any(option => option.Id == saved.SubtitleLanguages)
                ? saved.SubtitleLanguages
                : "en,ja,pt";
            Channels.Clear();
            foreach (var channel in saved.Channels)
            {
                var row = CreateRow();
                row.FolderName = string.IsNullOrWhiteSpace(channel.FolderName) ? "HD036" : channel.FolderName;
                row.ChannelUrl = channel.ChannelUrl ?? "";
                row.PlaylistStart = channel.PlaylistStart < 1 ? 1 : channel.PlaylistStart;
                row.PlaylistEnd = channel.PlaylistEnd < row.PlaylistStart ? row.PlaylistStart : channel.PlaylistEnd;
                row.NameStart = channel.NameStart < 1 ? 1 : channel.NameStart;
                Channels.Add(row);
            }

            if (Channels.Count == 0)
            {
                Channels.Add(CreateRow());
            }

            foreach (var row in Channels)
            {
                row.SetRoot(RootFolder);
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    private PackSettings Capture() => new()
    {
        YtDlpPath = YtDlpPath,
        CookiesPath = CookiesPath,
        RootFolder = RootFolder,
        Quality = Quality,
        Parallel = Parallel,
        OrderByViews = OrderByViews,
        DownloadSubtitles = DownloadSubtitles,
        SubtitleLanguages = SubtitleLanguages,
        Channels = Channels.Select(row => new PackChannelSettings
        {
            FolderName = row.FolderName,
            ChannelUrl = row.ChannelUrl,
            PlaylistStart = row.PlaylistStart,
            PlaylistEnd = row.PlaylistEnd,
            NameStart = row.NameStart
        }).ToList()
    };

    private PackChannelRowViewModel CreateRow()
    {
        var row = new PackChannelRowViewModel();
        row.SetRoot(RootFolder);
        row.SetDownloadSubtitles(DownloadSubtitles);
        row.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PackChannelRowViewModel.FolderName)
                or nameof(PackChannelRowViewModel.ChannelUrl)
                or nameof(PackChannelRowViewModel.PlaylistStart)
                or nameof(PackChannelRowViewModel.PlaylistEnd)
                or nameof(PackChannelRowViewModel.NameStart))
            {
                Persist();
            }
        };
        return row;
    }

    partial void OnOrderByViewsChanged(bool value) => Persist();

    partial void OnDownloadSubtitlesChanged(bool value)
    {
        foreach (var row in Channels)
        {
            row.SetDownloadSubtitles(value);
        }

        Persist();
    }

    partial void OnSubtitleLanguagesChanged(string value)
    {
        if (SubtitleLanguageOptions.All(option => option.Id != value))
        {
            SubtitleLanguages = "en,ja,pt";
            return;
        }

        Persist();
    }

    private void Persist()
    {
        if (!_restoring)
        {
            Save();
        }
    }

    private static string NextFolderName(string current)
    {
        var name = current.Trim();
        var index = name.Length;
        while (index > 0 && char.IsDigit(name[index - 1]))
        {
            index--;
        }

        if (index == name.Length || !int.TryParse(name[index..], out var number))
        {
            return name + "2";
        }

        var width = name.Length - index;
        return name[..index] + (number + 1).ToString().PadLeft(width, '0');
    }

    private static string? PickFile(string title, string filter, string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PickFolder(string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static string? DirectoryOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Directory.Exists(path) ? path : Path.GetDirectoryName(path);
    }
}

public sealed record PackQualityOption(string Id, string Label)
{
    public override string ToString() => Label;
}

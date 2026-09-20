using VideoAutoTool.Core.Logging;

namespace VideoAutoTool.Core.AutoMode;

/// <summary>
/// Monitors a folder for new video files and automatically adds them to the render queue.
/// </summary>
public sealed class AutoModeService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, DateTime> _pendingFiles = new();
    private readonly Timer _stabilityTimer;
    private readonly TimeSpan _stabilityDelay = TimeSpan.FromSeconds(10);
    private readonly ILog _log;
    private bool _disposed;

    public event EventHandler<VideoReadyEventArgs>? VideoReady;

    /// <summary>
    /// Gets whether auto mode is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    public AutoModeService(string driverFolder, ILog log)
    {
        _log = log;
        
        _watcher = new FileSystemWatcher(driverFolder)
        {
            Filter = "*.mp4",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            EnableRaisingEvents = false
        };

        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;

        _stabilityTimer = new Timer(CheckPendingFiles, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        if (IsRunning) return;

        _watcher.EnableRaisingEvents = true;
        _stabilityTimer.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        IsRunning = true;
        _log.Info($"[AutoMode] Started monitoring: {_watcher.Path}");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _watcher.EnableRaisingEvents = false;
        _stabilityTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _pendingFiles.Clear();
        IsRunning = false;
        _log.Info("[AutoMode] Stopped monitoring");
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _log.Info($"[AutoMode] Detected new file: {Path.GetFileName(e.FullPath)}");
        lock (_pendingFiles)
        {
            _pendingFiles[e.FullPath] = DateTime.UtcNow;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_pendingFiles)
        {
            if (_pendingFiles.ContainsKey(e.FullPath))
            {
                _pendingFiles[e.FullPath] = DateTime.UtcNow;
            }
        }
    }

    private void CheckPendingFiles(object? state)
    {
        List<string> readyFiles;
        
        lock (_pendingFiles)
        {
            var now = DateTime.UtcNow;
            readyFiles = _pendingFiles
                .Where(kvp => now - kvp.Value >= _stabilityDelay)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var file in readyFiles)
            {
                _pendingFiles.Remove(file);
            }
        }

        foreach (var videoPath in readyFiles)
        {
            if (!File.Exists(videoPath))
            {
                _log.Warn($"[AutoMode] File disappeared: {Path.GetFileName(videoPath)}");
                continue;
            }

            var srtPath = Path.ChangeExtension(videoPath, ".srt");
            if (!File.Exists(srtPath))
            {
                _log.Info($"[AutoMode] Waiting for SRT: {Path.GetFileName(videoPath)} (SRT not found yet)");
                continue;
            }

            _log.Info($"[AutoMode] Video ready: {Path.GetFileName(videoPath)}");
            VideoReady?.Invoke(this, new VideoReadyEventArgs(videoPath, srtPath));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _watcher.Dispose();
        _stabilityTimer.Dispose();
    }
}

public sealed class VideoReadyEventArgs : EventArgs
{
    public string VideoPath { get; }
    public string SrtPath { get; }

    public VideoReadyEventArgs(string videoPath, string srtPath)
    {
        VideoPath = videoPath;
        SrtPath = srtPath;
    }
}

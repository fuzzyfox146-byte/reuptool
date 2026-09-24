namespace VideoAutoTool.Core.Download;

public sealed record DownloadTools(string YtDlpPath, string FfmpegLocation, string CookiesPath);

public sealed record ChannelDownloadRequest(
    string ParentFolder,
    string ChannelUrl,
    int PlaylistStart,
    int PlaylistEnd,
    int NameStart,
    string Language,
    string Quality);

public enum DownloadStop
{
    Completed,
    Failed,
    Cancelled,
    CookieDead,
    StoppedBecauseCookie
}

public sealed record ChannelDownloadResult(string ParentFolder, DownloadStop Stop, string Detail);

public sealed record YtDlpRunResult(int ExitCode, bool CookieDead, string? CookieLine);

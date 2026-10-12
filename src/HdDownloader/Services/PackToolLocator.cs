namespace HdDownloader.Services;

public static class PackToolLocator
{
    public static string? FindYtDlp()
    {
        var candidate = Path.Combine(@"D:\Tải video", "yt-dlp.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    public static string? FindCookies(string? ytDlpPath)
    {
        if (!string.IsNullOrWhiteSpace(ytDlpPath))
        {
            var beside = Path.Combine(Path.GetDirectoryName(ytDlpPath) ?? "", "cookies.txt");
            if (File.Exists(beside))
            {
                return beside;
            }
        }

        var fallback = Path.Combine(@"D:\Tải video", "cookies.txt");
        return File.Exists(fallback) ? fallback : null;
    }

    public static string FindFfmpegDirectory(string? ytDlpPath)
    {
        if (!string.IsNullOrWhiteSpace(ytDlpPath))
        {
            var dir = Path.GetDirectoryName(ytDlpPath) ?? "";
            if (File.Exists(Path.Combine(dir, "bin", "ffmpeg.exe")))
            {
                return Path.Combine(dir, "bin");
            }

            if (File.Exists(Path.Combine(dir, "ffmpeg.exe")))
            {
                return dir;
            }
        }

        var known = Path.Combine(@"D:\Tải video", "bin");
        return File.Exists(Path.Combine(known, "ffmpeg.exe")) ? known : "";
    }
}

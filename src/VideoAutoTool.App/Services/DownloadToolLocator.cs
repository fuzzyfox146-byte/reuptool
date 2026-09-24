namespace VideoAutoTool.App.Services;

public static class DownloadToolLocator
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
}

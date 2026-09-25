namespace VideoAutoTool.Core.Ffmpeg;

/// <summary>
/// After ffmpeg prints progress=end it may still rewrite the file for +faststart.
/// Only treat the process as hung when the output size stops changing.
/// </summary>
public static class ProcessStallWatch
{
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(15);

    public static bool IsStalled(long lastSize, DateTime lastChangeUtc, long currentSize, DateTime nowUtc) =>
        currentSize == lastSize && nowUtc - lastChangeUtc >= StallTimeout;

    public static long TryReadLength(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return 0;
        }

        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}

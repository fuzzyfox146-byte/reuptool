namespace VideoAutoTool.Core.Render;

public static class RenderTiming
{
    public static TimeSpan? Elapsed(DateTime? startedAt, DateTime? finishedAt) =>
        startedAt is { } start && finishedAt is { } end && end >= start
            ? end - start
            : null;

    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours} giờ {elapsed.Minutes} phút {elapsed.Seconds:00} giây";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes} phút {elapsed.Seconds:00} giây";
        }

        var seconds = Math.Max(1, (int)Math.Round(elapsed.TotalSeconds));
        return $"{seconds} giây";
    }

    public static string Describe(TimeSpan elapsed, double videoDurationSeconds)
    {
        var text = FormatElapsed(elapsed);
        if (videoDurationSeconds <= 0 || elapsed.TotalSeconds <= 0)
        {
            return text;
        }

        var video = TimeSpan.FromSeconds(videoDurationSeconds);
        var videoLabel = video.TotalHours >= 1
            ? video.ToString(@"h\:mm\:ss")
            : video.ToString(@"mm\:ss");
        var speed = videoDurationSeconds / elapsed.TotalSeconds;
        return $"{text} (video {videoLabel}, {speed:0.0}×)";
    }
}

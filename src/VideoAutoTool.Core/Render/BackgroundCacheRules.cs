using VideoAutoTool.Core.Planning;

namespace VideoAutoTool.Core.Render;

/// <summary>
/// Cache files are keyed by the source clip length. Encode that full length so a
/// later, longer job can reuse the same file and trim at render time.
/// </summary>
public static class BackgroundCacheRules
{
    public static double EncodeDurationSeconds(BackgroundSegment segment) =>
        Math.Max(0.01, segment.DurationFull);
}

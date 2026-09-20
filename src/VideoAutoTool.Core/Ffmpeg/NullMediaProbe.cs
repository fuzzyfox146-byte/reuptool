namespace VideoAutoTool.Core.Ffmpeg;

public sealed class NullMediaProbe : IMediaProbe
{
    public Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ffmpeg is not configured.");
}

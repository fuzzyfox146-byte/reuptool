namespace VideoAutoTool.Core.Ffmpeg;

public interface IMediaProbe
{
    Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken = default);
}

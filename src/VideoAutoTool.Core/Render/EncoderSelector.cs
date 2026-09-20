using VideoAutoTool.Core.Ffmpeg;

namespace VideoAutoTool.Core.Render;

public enum VideoEncoderKind
{
    LibX264,
    H264Nvenc
}

public sealed class EncoderSelector
{
    private readonly FfmpegRunner _runner;
    private VideoEncoderKind? _cached;

    public EncoderSelector(FfmpegRunner runner) => _runner = runner;

    public async Task<VideoEncoderKind> SelectAsync(bool preferNvenc, CancellationToken cancellationToken = default)
    {
        if (!preferNvenc)
        {
            return VideoEncoderKind.LibX264;
        }

        if (_cached is not null)
        {
            return _cached.Value;
        }

        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "color=c=black:s=256x256:d=0.2",
            "-c:v", "h264_nvenc", "-f", "null", "-"
        };
        var result = await _runner.RunAsync(args, null, cancellationToken).ConfigureAwait(false);
        _cached = result.ExitCode == 0 ? VideoEncoderKind.H264Nvenc : VideoEncoderKind.LibX264;
        return _cached.Value;
    }
}

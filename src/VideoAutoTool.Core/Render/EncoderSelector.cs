using VideoAutoTool.Core.Ffmpeg;

namespace VideoAutoTool.Core.Render;

public enum VideoEncoderKind
{
    LibX264,
    H264Nvenc
}

public sealed record EncodeSettings(VideoEncoderKind Encoder, string? HwAccel);

public sealed class EncoderSelector
{
    private readonly FfmpegRunner _runner;
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private EncodeSettings? _cachedNvencProbe;

    public EncoderSelector(FfmpegRunner runner) => _runner = runner;

    public async Task<EncodeSettings> SelectAsync(bool preferNvenc, CancellationToken cancellationToken = default)
    {
        if (!preferNvenc)
        {
            var hwCpu = await DetectHwAccelAsync(preferCuda: false, cancellationToken).ConfigureAwait(false);
            return new EncodeSettings(VideoEncoderKind.LibX264, hwCpu);
        }

        if (_cachedNvencProbe is not null)
        {
            return _cachedNvencProbe;
        }

        await _probeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedNvencProbe is not null)
            {
                return _cachedNvencProbe;
            }

            var nvencArgs = new List<string>
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-f", "lavfi", "-i", "color=c=black:s=256x256:d=0.2",
                "-c:v", "h264_nvenc", "-f", "null", "-"
            };
            var nvenc = await _runner.RunAsync(nvencArgs, null, cancellationToken).ConfigureAwait(false);
            var encoder = nvenc.ExitCode == 0 ? VideoEncoderKind.H264Nvenc : VideoEncoderKind.LibX264;
            var hwAccel = await DetectHwAccelAsync(
                preferCuda: encoder == VideoEncoderKind.H264Nvenc,
                cancellationToken).ConfigureAwait(false);
            _cachedNvencProbe = new EncodeSettings(encoder, hwAccel);
            return _cachedNvencProbe;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    private async Task<string?> DetectHwAccelAsync(bool preferCuda, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            ["-hide_banner", "-hwaccels"],
            null,
            cancellationToken).ConfigureAwait(false);
        var text = (result.StandardOutput + Environment.NewLine + result.Tail).ToLowerInvariant();
        if (preferCuda && text.Contains("cuda", StringComparison.Ordinal))
        {
            return "cuda";
        }

        if (text.Contains("d3d11va", StringComparison.Ordinal))
        {
            return "d3d11va";
        }

        if (text.Contains("dxva2", StringComparison.Ordinal))
        {
            return "dxva2";
        }

        return preferCuda ? "cuda" : null;
    }
}

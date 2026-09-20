using VideoAutoTool.Core.Ffmpeg;

namespace VideoAutoTool.App.Services;

public sealed class FfmpegServices
{
    public FfmpegServices()
    {
        try
        {
            Paths = FfmpegLocator.Locate();
            Runner = new FfmpegRunner(Paths);
            Probe = new FfmpegProbe(Paths);
        }
        catch (FfmpegNotFoundException)
        {
            Paths = null;
            Runner = null;
            Probe = null;
        }
    }

    public FfmpegPaths? Paths { get; }

    public FfmpegRunner? Runner { get; }

    public FfmpegProbe? Probe { get; }
}

namespace VideoAutoTool.Core.Ffmpeg;

public sealed class FfmpegRunner
{
    private readonly FfmpegPaths _paths;

    public FfmpegRunner(FfmpegPaths paths) => _paths = paths;

    public Task<FfmpegResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        double? totalDurationSeconds = null) =>
        FfmpegProcessRunner.RunAsync(
            _paths.FfmpegPath,
            arguments,
            workingDirectory,
            cancellationToken,
            progress,
            totalDurationSeconds);
}

using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.FrameBatch;

namespace FrameRender;

public sealed class FfmpegCommandRunner : IFrameCommandRunner
{
    private readonly FfmpegRunner _runner;

    public FfmpegCommandRunner(FfmpegRunner runner) => _runner = runner;

    public async Task<FrameCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        double? durationSeconds = null,
        string? stallWatchPath = null)
    {
        var result = await _runner.RunAsync(arguments, null, cancellationToken, progress, durationSeconds, stallWatchPath)
            .ConfigureAwait(false);
        return new FrameCommandResult(result.ExitCode, result.Tail);
    }
}

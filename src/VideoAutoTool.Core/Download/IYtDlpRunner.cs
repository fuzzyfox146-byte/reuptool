namespace VideoAutoTool.Core.Download;

public interface IYtDlpRunner
{
    Task<YtDlpRunResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        IProgress<string>? log,
        CancellationToken cancellationToken);
}

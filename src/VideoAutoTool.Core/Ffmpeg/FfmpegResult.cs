namespace VideoAutoTool.Core.Ffmpeg;

public sealed record FfmpegResult(int ExitCode, string Tail, TimeSpan Elapsed, string StandardOutput = "");

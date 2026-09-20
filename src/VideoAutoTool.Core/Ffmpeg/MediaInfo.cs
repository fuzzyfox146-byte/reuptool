namespace VideoAutoTool.Core.Ffmpeg;

public sealed record MediaInfo(
    string Path,
    double? DurationSeconds,
    bool HasAudio,
    double? AudioDurationSeconds,
    bool HasVideo,
    int? Width,
    int? Height,
    double? Fps,
    string? PixelFormat,
    bool HasAlpha,
    string? Codec);

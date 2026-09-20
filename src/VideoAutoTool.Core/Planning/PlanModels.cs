using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Planning;

public enum PlanWarningLevel
{
    Warning,
    Info
}

public sealed record PlanWarning(PlanWarningLevel Level, string Message);

public sealed record BackgroundSegment(string File, double DurationFull);

public sealed record RenderJobPlan(
    int Index,
    string DriverPath,
    string DriverStem,
    string? SubPath,
    double DurationSeconds,
    IReadOnlyList<BackgroundSegment> Backgrounds,
    string? AvatarPath,
    string? WavePath,
    string Side,
    StylePreset StylePreset,
    string OutputPath);

public sealed record PlanResult(IReadOnlyList<RenderJobPlan> Jobs, IReadOnlyList<PlanWarning> Warnings);

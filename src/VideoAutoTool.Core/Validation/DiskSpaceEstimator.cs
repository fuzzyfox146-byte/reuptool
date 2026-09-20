using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Validation;

/// <summary>
/// Estimates output size for W072.
/// Formula: totalSeconds * estimatedVideoBitrateKbps / 8 * 1024 * 1.3 + audio.
/// Video bitrate heuristic from CRF/quality: max(800, 9000 - quality * 250) kbps at 720p.
/// </summary>
public static class DiskSpaceEstimator
{
    private const double SafetyFactor = 1.3;

    public static long EstimateBytes(Template template, double totalDurationSeconds)
    {
        var videoKbps = Math.Max(800, 9000 - template.Output.Quality * 250);
        var audioKbps = template.Output.AudioBitrateKbps;
        var totalKbps = videoKbps + audioKbps;
        return (long)(totalDurationSeconds * totalKbps * 1000 / 8 * SafetyFactor);
    }
}

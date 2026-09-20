using System.Globalization;
using System.Text.Json;

namespace VideoAutoTool.Core.Ffmpeg;

public static class MediaInfoParser
{
    private static readonly HashSet<string> AlphaPixelFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "rgba", "bgra", "argb", "abgr", "yuva420p", "yuva422p", "yuva444p", "yuva444p10le", "gbrap"
    };

    public static MediaInfo Parse(string path, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var format = root.GetProperty("format");
        var streams = root.GetProperty("streams");

        double? containerDuration = TryParseDouble(format, "duration");
        JsonElement? videoStream = null;
        JsonElement? audioStream = null;

        foreach (var stream in streams.EnumerateArray())
        {
            var type = stream.GetProperty("codec_type").GetString();
            if (type == "video" && videoStream is null)
            {
                videoStream = stream;
            }
            else if (type == "audio" && audioStream is null)
            {
                audioStream = stream;
            }
        }

        var hasVideo = videoStream is not null;
        var hasAudio = audioStream is not null;
        int? width = null;
        int? height = null;
        double? fps = null;
        string? pixelFormat = null;
        string? codec = null;
        var hasAlpha = false;

        if (videoStream is not null)
        {
            var vs = videoStream.Value;
            width = TryParseInt(vs, "width");
            height = TryParseInt(vs, "height");
            pixelFormat = TryGetString(vs, "pix_fmt");
            codec = TryGetString(vs, "codec_name");
            hasAlpha = pixelFormat is not null && AlphaPixelFormats.Contains(pixelFormat);
            fps = ParseFps(vs);
        }

        double? audioDuration = null;
        if (audioStream is not null)
        {
            audioDuration = TryParseDouble(audioStream.Value, "duration") ?? containerDuration;
        }

        return new MediaInfo(
            path,
            containerDuration,
            hasAudio,
            audioDuration,
            hasVideo,
            width,
            height,
            fps,
            pixelFormat,
            hasAlpha,
            codec);
    }

    private static double? ParseFps(JsonElement stream)
    {
        if (stream.TryGetProperty("avg_frame_rate", out var avg) && avg.GetString() is { } avgText && avgText.Contains('/'))
        {
            var parts = avgText.Split('/');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
                den > 0)
            {
                return num / den;
            }
        }

        if (stream.TryGetProperty("r_frame_rate", out var rf) && rf.GetString() is { } rfText && rfText.Contains('/'))
        {
            var parts = rfText.Split('/');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
                den > 0)
            {
                return num / den;
            }
        }

        return null;
    }

    private static double? TryParseDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return null;
    }

    private static int? TryParseInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return null;
    }

    private static string? TryGetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() : null;
}

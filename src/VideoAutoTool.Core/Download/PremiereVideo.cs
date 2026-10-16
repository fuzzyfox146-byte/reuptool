using System.Text.Json;

namespace VideoAutoTool.Core.Download;

public static class PremiereVideo
{
    public static bool? IsReady(string? ffprobeJson)
    {
        if (string.IsNullOrWhiteSpace(ffprobeJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(ffprobeJson);
            if (!document.RootElement.TryGetProperty("streams", out var streams) || streams.GetArrayLength() == 0)
            {
                return null;
            }

            var sawVideo = false;
            var videoOk = false;
            var audioOk = true;
            foreach (var stream in streams.EnumerateArray())
            {
                var type = stream.TryGetProperty("codec_type", out var typeEl) ? typeEl.GetString() : null;
                var codec = stream.TryGetProperty("codec_name", out var codecEl) ? codecEl.GetString() : null;
                if (type == "video")
                {
                    sawVideo = true;
                    var pix = stream.TryGetProperty("pix_fmt", out var pixEl) ? pixEl.GetString() : null;
                    videoOk = codec == "h264" && pix is "yuv420p" or "yuvj420p";
                }
                else if (type == "audio")
                {
                    audioOk = codec == "aac";
                }
            }

            return sawVideo && videoOk && audioOk;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> ProbeArgs(string mediaPath) =>
    [
        "-v", "error",
        "-show_entries", "stream=codec_type,codec_name,pix_fmt",
        "-of", "json",
        mediaPath
    ];

    public static IReadOnlyList<string> TranscodeArgs(string input, string output) =>
    [
        "-y",
        "-i", input,
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-profile:v", "high",
        "-preset", "veryfast",
        "-crf", "20",
        "-c:a", "aac",
        "-b:a", "192k",
        "-movflags", "+faststart",
        output
    ];
}

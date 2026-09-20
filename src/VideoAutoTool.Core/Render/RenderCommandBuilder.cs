using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Render;

public static class RenderCommandBuilder
{
    public static List<string> BuildArguments(
        Template template,
        RenderJobPlan job,
        FilterGraphPlan graph,
        string driverPath,
        string outputPath,
        VideoEncoderKind encoder,
        RenderRequest request)
    {
        var duration = request.Mode switch
        {
            RenderMode.Clip => Math.Min(request.ClipSeconds ?? 10, job.DurationSeconds),
            RenderMode.Frame => (request.FrameTimeSeconds ?? 0) + 0.5,
            _ => job.DurationSeconds
        };

        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "warning", "-nostdin",
            "-i", Path.GetFullPath(driverPath)
        };

        foreach (var input in graph.ExtraInputs)
        {
            if (input.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                input.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("-loop");
                args.Add("1");
                args.Add("-framerate");
                args.Add(template.Canvas.Fps.ToString());
            }
            else if (input == job.WavePath)
            {
                args.Add("-stream_loop");
                args.Add("-1");
            }

            args.Add("-i");
            args.Add(Path.GetFullPath(input));
        }

        args.Add("-filter_complex");
        args.Add(graph.FilterComplex.Replace(job.DurationSeconds.ToString("0.###"), duration.ToString("0.###")));
        args.Add("-map");
        args.Add(graph.VideoOutputLabel);
        args.Add("-map");
        args.Add("0:a:0");
        args.Add("-t");
        args.Add(duration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

        AppendVideoEncoder(args, encoder, template.Output.Quality);
        args.Add("-c:a");
        args.Add("aac");
        args.Add("-b:a");
        args.Add($"{template.Output.AudioBitrateKbps}k");
        args.Add("-ar");
        args.Add("48000");
        args.Add("-colorspace");
        args.Add("bt709");
        args.Add("-color_primaries");
        args.Add("bt709");
        args.Add("-color_trc");
        args.Add("bt709");
        args.Add("-color_range");
        args.Add("tv");
        args.Add("-movflags");
        args.Add("+faststart");
        args.Add("-f");
        args.Add("mp4");
        args.Add("-progress");
        args.Add("pipe:1");
        args.Add("-nostats");
        args.Add(Path.GetFullPath(outputPath));
        return args;
    }

    public static List<string> BuildPreviewArguments(
        Template template,
        RenderJobPlan job,
        FilterGraphPlan graph,
        string driverPath,
        string outputPath,
        double frameTime)
    {
        var previewDuration = frameTime + 0.5;
        var filter = graph.FilterComplex
            .Replace(job.DurationSeconds.ToString("0.###"), previewDuration.ToString("0.###"))
            .Replace("format=yuv420p[vout]", "scale=800:-2:flags=bicubic,format=rgb24[vout]");

        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "warning", "-nostdin",
            "-i", Path.GetFullPath(driverPath)
        };

        foreach (var input in graph.ExtraInputs)
        {
            if (input.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                input.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                args.AddRange(["-loop", "1", "-framerate", template.Canvas.Fps.ToString()]);
            }
            else if (input == job.WavePath)
            {
                args.AddRange(["-stream_loop", "-1"]);
            }

            args.Add("-i");
            args.Add(Path.GetFullPath(input));
        }

        args.AddRange([
            "-filter_complex", filter,
            "-map", graph.VideoOutputLabel,
            "-t", previewDuration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-ss", frameTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-frames:v", "1",
            "-f", "image2", "-update", "1",
            Path.GetFullPath(outputPath)
        ]);
        return args;
    }

    private static void AppendVideoEncoder(List<string> args, VideoEncoderKind encoder, int quality)
    {
        if (encoder == VideoEncoderKind.H264Nvenc)
        {
            args.AddRange(["-c:v", "h264_nvenc", "-preset", "p5", "-rc", "vbr", "-cq", quality.ToString(), "-b:v", "0", "-profile:v", "high"]);
        }
        else
        {
            args.AddRange(["-c:v", "libx264", "-preset", "medium", "-crf", quality.ToString(), "-profile:v", "high"]);
        }
    }
    public static List<string> BuildCachedArguments(
        Template template,
        RenderJobPlan job,
        FilterGraphPlan graph,
        string driverPath,
        string concatListPath,
        string outputPath,
        VideoEncoderKind encoder,
        RenderRequest request)
    {
        var duration = request.Mode switch
        {
            RenderMode.Clip => Math.Min(request.ClipSeconds ?? 10, job.DurationSeconds),
            RenderMode.Frame => (request.FrameTimeSeconds ?? 0) + 0.5,
            _ => job.DurationSeconds
        };

        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "warning", "-nostdin",
            "-i", Path.GetFullPath(driverPath),
            "-f", "concat", "-safe", "0", "-i", Path.GetFullPath(concatListPath)
        };

        foreach (var input in graph.ExtraInputs.Skip(1))
        {
            if (input.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                input.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("-loop");
                args.Add("1");
                args.Add("-framerate");
                args.Add(template.Canvas.Fps.ToString());
            }
            else if (input == job.WavePath)
            {
                args.Add("-stream_loop");
                args.Add("-1");
            }

            args.Add("-i");
            args.Add(Path.GetFullPath(input));
        }

        args.Add("-filter_complex");
        args.Add(graph.FilterComplex);
        args.Add("-map");
        args.Add(graph.VideoOutputLabel);
        args.Add("-map");
        args.Add("0:a:0");

        AppendVideoEncoder(args, encoder, template.Output.Quality);

        args.Add("-shortest");
        args.Add("-movflags");
        args.Add("+faststart");
        args.Add(Path.GetFullPath(outputPath));

        return args;
    }
}
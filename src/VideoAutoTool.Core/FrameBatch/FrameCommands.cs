namespace VideoAutoTool.Core.FrameBatch;

public static class FrameCommands
{
    public static IReadOnlyList<string> FrameCache(string source, string destination, FrameSettings settings) =>
    [
        "-y", "-hide_banner", "-loglevel", "error", "-nostdin",
        "-i", source,
        "-vf", $"scale={settings.Width}:{settings.Height}:flags=lanczos,format=rgba",
        "-frames:v", "1",
        destination
    ];

    public static IReadOnlyList<string> BackgroundCache(string source, string destination, FrameSettings settings) =>
    [
        "-y", "-hide_banner", "-loglevel", "error", "-nostdin",
        "-i", source,
        "-an",
        "-vf", $"scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=increase,crop={settings.Width}:{settings.Height},fps={settings.Fps},setsar=1",
        "-c:v", "libx264", "-crf", "14", "-preset", "fast", "-g", settings.Fps.ToString(),
        "-pix_fmt", "yuv420p",
        destination
    ];

    public static IReadOnlyList<string> NoiseCache(string source, string destination, FrameSettings settings)
    {
        var speed = settings.NoiseSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var filter =
            $"scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=increase," +
            $"crop={settings.Width}:{settings.Height},setpts=PTS/{speed},fps={settings.Fps},setsar=1";
        return
        [
            "-y", "-hide_banner", "-loglevel", "error", "-nostdin",
            "-i", source,
            "-an",
            "-vf", filter,
            "-c:v", "libx264", "-crf", "8", "-preset", "fast", "-g", settings.Fps.ToString(),
            "-pix_fmt", "yuv420p",
            destination
        ];
    }

    public static string Filter(FrameSettings settings, FrameLayout layout)
    {
        var opacity = settings.VideoOpacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return
            $"[0:v]fps={settings.Fps},setpts=PTS-STARTPTS," +
            $"scale={layout.VideoW}:{layout.VideoH}:force_original_aspect_ratio=increase," +
            $"crop={layout.VideoW}:{layout.VideoH},setsar=1,format=rgba,colorchannelmixer=aa={opacity}[fg];" +
            $"[1:v]fps={settings.Fps},setpts=PTS-STARTPTS," +
            $"scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=increase," +
            $"crop={settings.Width}:{settings.Height},setsar=1[bg];" +
            $"[bg][fg]overlay={layout.X}:{layout.Y}:format=auto[m1];" +
            $"[2:v]format=rgba,fps={settings.Fps},setpts=PTS-STARTPTS[fr];" +
            $"[m1][fr]overlay=0:0:format=auto,format=yuv420p[out]";
    }

    /// <summary>
    /// CUDA decode, scale, overlay and NVENC. Opacity stays in the formula: overlay_cuda
    /// has no alpha knob, so only the smaller foreground is downloaded to set it.
    /// </summary>
    public static string FilterGpu(FrameSettings settings, FrameLayout layout)
    {
        var opacity = settings.VideoOpacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return
            $"[1:v]scale_cuda={settings.Width}:{settings.Height}:format=yuv420p[bg];" +
            $"[0:v]scale_cuda={layout.VideoW}:{layout.VideoH}:format=nv12:force_original_aspect_ratio=increase:force_divisible_by=2," +
            $"hwdownload,format=nv12,format=rgba,colorchannelmixer=aa={opacity},format=yuva420p,hwupload_cuda[fg];" +
            $"[bg][fg]overlay_cuda=x={layout.X}:y={layout.Y}:eof_action=repeat[m1];" +
            $"[2:v]format=yuva420p,hwupload_cuda[fr];" +
            $"[m1][fr]overlay_cuda=x=0:y=0:eof_action=repeat[out]";
    }

    public static IReadOnlyList<string> Render(
        string mainVideo,
        string background,
        string frame,
        string destination,
        double duration,
        bool withAudio,
        FrameSettings settings,
        FrameLayout layout)
    {
        var graph = settings.Gpu ? FilterGpu(settings, layout) : Filter(settings, layout);
        if (settings.Gpu && withAudio)
        {
            var seconds = Math.Max(0.1, duration).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            graph += $";[0:a]atrim=duration={seconds},asetpts=PTS-STARTPTS,aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo[aout]";
        }

        var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-nostdin" };
        if (settings.Gpu)
        {
            AddCudaInput(args, mainVideo, loop: false);
            AddCudaInput(args, background, loop: true);
        }
        else
        {
            args.Add("-i");
            args.Add(mainVideo);
            args.Add("-stream_loop");
            args.Add("-1");
            args.Add("-i");
            args.Add(background);
        }

        args.Add("-loop");
        args.Add("1");
        args.Add("-framerate");
        args.Add(settings.Fps.ToString());
        args.Add("-i");
        args.Add(frame);
        args.Add("-filter_complex");
        args.Add(graph);
        args.Add("-map");
        args.Add("[out]");
        if (withAudio)
        {
            args.Add("-map");
            args.Add(settings.Gpu ? "[aout]" : "0:a:0");
            args.Add("-c:a");
            args.Add("aac");
            args.Add("-b:a");
            args.Add("192k");
            if (settings.Gpu)
            {
                args.Add("-ar");
                args.Add("48000");
                args.Add("-max_muxing_queue_size");
                args.Add("200000");
                args.Add("-muxing_queue_data_threshold");
                args.Add("1073741824");
            }
        }
        else
        {
            args.Add("-an");
        }

        args.Add("-t");
        args.Add(duration.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        args.Add("-r");
        args.Add(settings.Fps.ToString());
        args.Add("-threads");
        args.Add((settings.Gpu ? Math.Min(2, Math.Max(1, settings.Threads)) : settings.Threads).ToString());
        args.Add("-progress");
        args.Add("pipe:1");
        args.Add("-nostats");
        if (settings.Gpu)
        {
            args.Add("-c:v");
            args.Add("h264_nvenc");
            args.Add("-preset");
            args.Add("p1");
            args.Add("-tune");
            args.Add("hq");
            args.Add("-rc");
            args.Add("vbr");
            args.Add("-cq");
            args.Add(settings.Crf.ToString());
            args.Add("-b:v");
            args.Add("0");
            args.Add("-bf");
            args.Add("0");
            args.Add("-profile:v");
            args.Add("high");
        }
        else
        {
            args.Add("-c:v");
            args.Add("libx264");
            args.Add("-preset");
            args.Add(settings.Preset);
            args.Add("-crf");
            args.Add(settings.Crf.ToString());
            args.Add("-profile:v");
            args.Add("high");
        }

        if (!settings.Gpu)
        {
            args.Add("-pix_fmt");
            args.Add("yuv420p");
        }
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
        args.Add(destination);
        return args;
    }

    private static void AddCudaInput(List<string> args, string path, bool loop)
    {
        if (loop)
        {
            args.Add("-stream_loop");
            args.Add("-1");
        }

        args.Add("-hwaccel");
        args.Add("cuda");
        args.Add("-hwaccel_output_format");
        args.Add("cuda");
        args.Add("-i");
        args.Add(path);
    }
}

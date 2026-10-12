namespace VideoAutoTool.Core.FrameBatch;

public static class FrameDesign
{
    public const int Width = 1280;
    public const int Height = 720;
    public const int Fps = 25;
    public const int RefWidth = 1672;
    public const int RefHeight = 941;
    public const int HoleX = 190;
    public const int HoleY = 117;
    public const int HoleW = 1296;
    public const int HoleH = 706;
    public const double VideoScale = 0.80;
    public const double VideoOpacity = 0.90;
    public const double NoiseOpacity = 0.30;
    public const double NoiseSpeed = 0.5;
    public const double NoiseSeconds = 10;
    public const string NoiseFile = @"C:\Noise effect\1.mp4";
    public const int Crf = 20;
    public const string Preset = "medium";
    public const int MaxParallelVideos = 16;
    public const string Wait = "Chờ";
    public const string Running = "Đang chạy";
    public const string Done = "Xong";
    public const string Failed = "Lỗi";
    public const string Skipped = "Bỏ qua";
    public const string Cancelled = "Đã hủy";
    public const string Paused = "Tạm dừng";

    public static readonly string[] VideoExtensions = [".mp4", ".mkv", ".mov", ".webm", ".m4v"];
    public static readonly string[] FrameExtensions = [".png"];

    public static int ParallelVideos(int requested) => Math.Clamp(requested, 1, MaxParallelVideos);
}

public sealed record FrameSettings(
    int Width = FrameDesign.Width,
    int Height = FrameDesign.Height,
    int Fps = FrameDesign.Fps,
    double VideoScale = FrameDesign.VideoScale,
    double VideoOpacity = FrameDesign.VideoOpacity,
    double NoiseOpacity = FrameDesign.NoiseOpacity,
    double NoiseSpeed = FrameDesign.NoiseSpeed,
    double NoiseSeconds = FrameDesign.NoiseSeconds,
    int Crf = FrameDesign.Crf,
    string Preset = FrameDesign.Preset,
    bool Gpu = false,
    int Workers = 1,
    int Threads = 1);

public readonly record struct FrameLayout(int VideoW, int VideoH, int X, int Y)
{
    public int CenterX => X + VideoW / 2;

    public int CenterY => Y + VideoH / 2;
}

public static class FrameLayoutMath
{
    public static int Even(double value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.ToEven);
        if (rounded % 2 != 0)
        {
            rounded += value >= rounded ? 1 : -1;
        }

        return Math.Max(rounded, 2);
    }

    public static FrameLayout Create(FrameSettings settings)
    {
        var videoW = Even(settings.Width * settings.VideoScale);
        var videoH = Even(videoW * 9.0 / 16.0);
        var centerX = (FrameDesign.HoleX + FrameDesign.HoleW / 2.0) * settings.Width / FrameDesign.RefWidth;
        var centerY = (FrameDesign.HoleY + FrameDesign.HoleH / 2.0) * settings.Height / FrameDesign.RefHeight;
        var x = (int)Math.Round(centerX - videoW / 2.0, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(centerY - videoH / 2.0, MidpointRounding.AwayFromZero);
        return new FrameLayout(videoW, videoH, x, y);
    }

    public static string NormalizeStatus(string? status) =>
        status is FrameDesign.Running ? FrameDesign.Wait : status ?? FrameDesign.Wait;
}

public readonly record struct FolderRenderCount(string Name, int Done, int Total);

public static class FrameFolderProgress
{
    public static string Key(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "(không tên)" : name;

    public static IReadOnlyList<FolderRenderCount> Count(
        IEnumerable<(string Name, string State)> jobs,
        IReadOnlyDictionary<string, int>? alreadyDone = null)
    {
        var order = new List<string>();
        var done = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, state) in jobs)
        {
            var key = Key(name);
            if (total.TryAdd(key, 0))
            {
                order.Add(key);
                done[key] = 0;
            }

            total[key]++;
            if (state is FrameDesign.Done or FrameDesign.Skipped)
            {
                done[key]++;
            }
        }

        if (alreadyDone is not null)
        {
            foreach (var pair in alreadyDone)
            {
                var key = Key(pair.Key);
                if (total.TryAdd(key, 0))
                {
                    order.Add(key);
                    done[key] = 0;
                }

                done[key] += pair.Value;
                total[key] += pair.Value;
            }
        }

        return order.Select(name => new FolderRenderCount(name, done[name], total[name])).ToList();
    }

    public static string Format(IEnumerable<(string Name, string State)> jobs, IReadOnlyDictionary<string, int>? alreadyDone = null) =>
        string.Join(Environment.NewLine, Count(jobs, alreadyDone).Select(item => $"{item.Name} {item.Done}/{item.Total}"));
}

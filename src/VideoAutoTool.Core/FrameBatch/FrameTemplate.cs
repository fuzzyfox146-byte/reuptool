using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VideoAutoTool.Core.Subtitles;

namespace VideoAutoTool.Core.FrameBatch;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrameLayerKind
{
    BackgroundVideo,
    MainVideo,
    FrameOverlay,
    ImageOverlay,
    LoopVideo,
    Subtitle,
    StaticText
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrameLayerPick
{
    RoundRobin,
    Random,
    Fixed,
    MatchByNumber
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrameKeyMode
{
    None,
    BlackKey,
    GreenKey,
    CustomColorKey
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrameMirrorMode
{
    None,
    Left,
    Right,
    Alternate
}

public sealed class FrameTemplatePreset
{
    public string Name { get; set; } = "Mặc định (Nền + Video + Khung)";

    public bool AudioOnlyDriver { get; set; }

    /// <summary>Shared folder scanned by layers whose queue source mode is AutoDetect.</summary>
    public string AutoSourceFolder { get; set; } = "";

    public FrameMirrorMode MirrorSide { get; set; } = FrameMirrorMode.None;

    public double AudioSpeed { get; set; } = 1.0;

    public double AudioPitchSemitones { get; set; }

    public double AudioVolume { get; set; } = 1.0;

    public List<FrameTemplateLayer> Layers { get; set; } = [];

    public FrameTemplatePreset Clone()
    {
        var copy = new FrameTemplatePreset
        {
            Name = Name,
            AudioOnlyDriver = AudioOnlyDriver,
            AutoSourceFolder = AutoSourceFolder,
            MirrorSide = MirrorSide,
            AudioSpeed = AudioSpeed,
            AudioPitchSemitones = AudioPitchSemitones,
            AudioVolume = AudioVolume,
            Layers = Layers.Select(layer => layer.Clone()).ToList()
        };
        return copy;
    }

    public static FrameTemplatePreset CreateDefault(
        string framesFolder = "",
        string backgroundsFolder = "",
        double videoScale = FrameDesign.VideoScale,
        double videoOpacity = FrameDesign.VideoOpacity,
        bool random = false)
    {
        var pick = random ? FrameLayerPick.Random : FrameLayerPick.RoundRobin;
        return new FrameTemplatePreset
        {
            Name = "Mặc định (Nền + Video + Khung)",
            Layers =
            [
                new FrameTemplateLayer
                {
                    Id = "bg",
                    Name = "Video nền",
                    Kind = FrameLayerKind.BackgroundVideo,
                    Enabled = true,
                    Folder = backgroundsFolder,
                    Pick = pick,
                    FullCanvas = true,
                    Opacity = 1.0
                },
                new FrameTemplateLayer
                {
                    Id = "main",
                    Name = "Video chính",
                    Kind = FrameLayerKind.MainVideo,
                    Enabled = true,
                    UseReferenceHole = true,
                    Scale = videoScale,
                    Opacity = videoOpacity
                },
                new FrameTemplateLayer
                {
                    Id = "frame",
                    Name = "Khung PNG",
                    Kind = FrameLayerKind.FrameOverlay,
                    Enabled = true,
                    Folder = framesFolder,
                    Pick = pick,
                    FullCanvas = true,
                    Opacity = 1.0
                }
            ]
        };
    }

    public static FrameTemplatePreset CreateStoryPreset(
        string backgroundsFolder = "",
        string avatarsFolder = "",
        string wavesFolder = "",
        string subsFolder = "")
    {
        return new FrameTemplatePreset
        {
            Name = "Truyện / Audio + Avatar + Wave + Sub",
            AudioOnlyDriver = true,
            MirrorSide = FrameMirrorMode.Alternate,
            Layers =
            [
                new FrameTemplateLayer
                {
                    Id = "bg",
                    Name = "Video nền",
                    Kind = FrameLayerKind.BackgroundVideo,
                    Enabled = true,
                    Folder = backgroundsFolder,
                    Pick = FrameLayerPick.RoundRobin,
                    FullCanvas = true,
                    Opacity = 0.65
                },
                new FrameTemplateLayer
                {
                    Id = "avatar",
                    Name = "Avatar PNG",
                    Kind = FrameLayerKind.ImageOverlay,
                    Enabled = true,
                    Folder = avatarsFolder,
                    Pick = FrameLayerPick.RoundRobin,
                    X = 760,
                    Y = 60,
                    Width = 500,
                    Height = 660,
                    MirrorWithSide = true,
                    Opacity = 1.0
                },
                new FrameTemplateLayer
                {
                    Id = "wave",
                    Name = "Sóng nhạc (Wave)",
                    Kind = FrameLayerKind.LoopVideo,
                    Enabled = true,
                    Folder = wavesFolder,
                    Pick = FrameLayerPick.RoundRobin,
                    X = 140,
                    Y = 230,
                    Width = 480,
                    Height = 140,
                    KeyMode = FrameKeyMode.BlackKey,
                    KeyTolerance = 0.15,
                    MirrorWithSide = true,
                    Opacity = 1.0
                },
                new FrameTemplateLayer
                {
                    Id = "sub",
                    Name = "Phụ đề (SRT)",
                    Kind = FrameLayerKind.Subtitle,
                    Enabled = true,
                    Folder = subsFolder,
                    Pick = FrameLayerPick.MatchByNumber,
                    X = 400,
                    Y = 540,
                    Width = 540,
                    Height = 200,
                    FontName = "Arial",
                    FontSize = 48,
                    TextColor = "#F5A623,#FFFFFF,#00E5FF",
                    OutlineColor = "#000000",
                    OutlineWidth = 3.0,
                    ShadowDepth = 1.5,
                    MirrorWithSide = true
                }
            ]
        };
    }

    public bool IsStandardThreeLayer()
    {
        if (AudioOnlyDriver
            || Math.Abs(AudioSpeed - 1.0) > 0.001
            || Math.Abs(AudioPitchSemitones) > 0.001
            || Math.Abs(AudioVolume - 1.0) > 0.001)
        {
            return false;
        }

        var active = Layers.Where(layer => layer.Enabled).ToList();
        if (active.Count != 3)
        {
            return false;
        }

        return active[0].Kind == FrameLayerKind.BackgroundVideo && active[0].HasNoCustomEffects()
            && active[1].Kind == FrameLayerKind.MainVideo && active[1].UseReferenceHole && active[1].HasNoCustomEffects()
            && active[2].Kind == FrameLayerKind.FrameOverlay && active[2].HasNoCustomEffects();
    }
}

public sealed class FrameTemplateLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public string Name { get; set; } = "Layer";

    public FrameLayerKind Kind { get; set; } = FrameLayerKind.ImageOverlay;

    public bool Enabled { get; set; } = true;

    public bool Locked { get; set; }

    public string Folder { get; set; } = "";

    public string FixedFile { get; set; } = "";

    public FrameLayerPick Pick { get; set; } = FrameLayerPick.RoundRobin;

    /// <summary>Where the queue gets this layer's files (set on the queue tab, not in the designer).</summary>
    public LayerSourceMode QueueSourceMode { get; set; } = LayerSourceMode.FromTemplate;

    /// <summary>Folder, file or link typed on the queue tab; meaning depends on <see cref="QueueSourceMode"/>.</summary>
    public string QueueSourcePath { get; set; } = "";

    /// <summary>'|' separated keywords; when set only files whose name contains one of them are used (AutoDetect).</summary>
    public string NameFilter { get; set; } = "";

    public bool FullCanvas { get; set; }

    public bool UseReferenceHole { get; set; }

    public int X { get; set; } = 100;

    public int Y { get; set; } = 100;

    public int Width { get; set; } = 400;

    public int Height { get; set; } = 300;

    public double Scale { get; set; } = 0.80;

    public double Opacity { get; set; } = 1.0;

    public double CropLeft { get; set; }

    public double CropRight { get; set; }

    public double CropTop { get; set; }

    public double CropBottom { get; set; }

    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    public bool MirrorWithSide { get; set; }

    public FrameKeyMode KeyMode { get; set; } = FrameKeyMode.None;

    public string KeyColor { get; set; } = "#00FF00";

    public double KeyTolerance { get; set; } = 0.18;

    public double KeySoftness { get; set; } = 0.08;

    public double Blur { get; set; }

    public double Brightness { get; set; }

    public double Contrast { get; set; } = 1.0;

    public double Saturation { get; set; } = 1.0;

    public double Speed { get; set; } = 1.0;

    public string FontName { get; set; } = "Arial";

    public int FontSize { get; set; } = 44;

    public string TextColor { get; set; } = "#FFFFFF";

    public string OutlineColor { get; set; } = "#000000";

    public double OutlineWidth { get; set; } = 2.5;

    public double ShadowDepth { get; set; } = 1.0;

    public string StaticText { get; set; } = "";

    public bool StripEmoji { get; set; } = true;

    public bool Uppercase { get; set; }

    public bool HasNoCustomEffects() =>
        CropLeft <= 0.001
        && CropRight <= 0.001
        && CropTop <= 0.001
        && CropBottom <= 0.001
        && !FlipHorizontal
        && !FlipVertical
        && !MirrorWithSide
        && KeyMode == FrameKeyMode.None
        && Blur <= 0.001
        && Math.Abs(Brightness) <= 0.001
        && Math.Abs(Contrast - 1.0) <= 0.001
        && Math.Abs(Saturation - 1.0) <= 0.001
        && Math.Abs(Speed - 1.0) <= 0.001;

    public FrameTemplateLayer Clone() => new()
    {
        Id = Id,
        Name = Name,
        Kind = Kind,
        Enabled = Enabled,
        Locked = Locked,
        Folder = Folder,
        FixedFile = FixedFile,
        Pick = Pick,
        QueueSourceMode = QueueSourceMode,
        QueueSourcePath = QueueSourcePath,
        NameFilter = NameFilter,
        FullCanvas = FullCanvas,
        UseReferenceHole = UseReferenceHole,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        Scale = Scale,
        Opacity = Opacity,
        CropLeft = CropLeft,
        CropRight = CropRight,
        CropTop = CropTop,
        CropBottom = CropBottom,
        FlipHorizontal = FlipHorizontal,
        FlipVertical = FlipVertical,
        MirrorWithSide = MirrorWithSide,
        KeyMode = KeyMode,
        KeyColor = KeyColor,
        KeyTolerance = KeyTolerance,
        KeySoftness = KeySoftness,
        Blur = Blur,
        Brightness = Brightness,
        Contrast = Contrast,
        Saturation = Saturation,
        Speed = Speed,
        FontName = FontName,
        FontSize = FontSize,
        TextColor = TextColor,
        OutlineColor = OutlineColor,
        OutlineWidth = OutlineWidth,
        ShadowDepth = ShadowDepth,
        StaticText = StaticText,
        StripEmoji = StripEmoji,
        Uppercase = Uppercase
    };
}

public sealed record ResolvedLayerAsset(
    FrameTemplateLayer Layer,
    string? AssetPath,
    string ChosenTextColor);

public sealed record ResolvedTemplateJob(
    string DriverVideo,
    string OutputName,
    bool MirroredLeft,
    IReadOnlyList<ResolvedLayerAsset> Layers)
{
    public string PrimaryBackground =>
        Layers.FirstOrDefault(item => item.Layer.Kind == FrameLayerKind.BackgroundVideo)?.AssetPath ?? "";

    public string PrimaryFrame =>
        Layers.FirstOrDefault(item => item.Layer.Kind == FrameLayerKind.FrameOverlay)?.AssetPath
        ?? Layers.FirstOrDefault(item => item.Layer.Kind == FrameLayerKind.ImageOverlay)?.AssetPath
        ?? "";
}

public static class FrameTemplatePlanner
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    private static readonly string[] SubExtensions = [".srt", ".ass"];
    private static readonly Regex LeadingNumberRegex = new(@"^\s*(\d+)", RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtensionsFor(FrameLayerKind kind) => kind switch
    {
        FrameLayerKind.BackgroundVideo or FrameLayerKind.LoopVideo => FrameDesign.VideoExtensions,
        FrameLayerKind.MainVideo => FrameDesign.DriverExtensions,
        FrameLayerKind.FrameOverlay or FrameLayerKind.ImageOverlay => ImageExtensions,
        FrameLayerKind.Subtitle => SubExtensions,
        _ => []
    };

    public static IReadOnlyList<ResolvedTemplateJob> AssignJobs(
        IReadOnlyList<string> drivers,
        FrameTemplatePreset preset,
        int baseSeed)
    {
        var layerPools = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var layerIndex = 0;
        foreach (var layer in preset.Layers.Where(layer => layer.Enabled))
        {
            layerIndex++;
            if (layer.Kind is FrameLayerKind.MainVideo or FrameLayerKind.StaticText)
            {
                continue;
            }

            if (layer.Pick == FrameLayerPick.Fixed && !string.IsNullOrWhiteSpace(layer.FixedFile) && File.Exists(layer.FixedFile))
            {
                layerPools[layer.Id] = [Path.GetFullPath(layer.FixedFile)];
                continue;
            }

            if (string.IsNullOrWhiteSpace(layer.Folder) || !Directory.Exists(layer.Folder))
            {
                layerPools[layer.Id] = [];
                continue;
            }

            var files = FramePlan.ListMedia(layer.Folder, ExtensionsFor(layer.Kind)).ToList();
            if (!string.IsNullOrWhiteSpace(layer.NameFilter))
            {
                files = files.Where(file => FrameSourceResolver.MatchesFilter(file, layer.NameFilter)).ToList();
            }

            if (layer.Pick == FrameLayerPick.Random && files.Count > 1)
            {
                Shuffle(files, baseSeed + layerIndex * 97);
            }

            layerPools[layer.Id] = files;
        }

        var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ResolvedTemplateJob>(drivers.Count);
        for (var i = 0; i < drivers.Count; i++)
        {
            var driver = Path.GetFullPath(drivers[i]);
            var mirroredLeft = preset.MirrorSide switch
            {
                FrameMirrorMode.Left => true,
                FrameMirrorMode.Right => false,
                FrameMirrorMode.Alternate => i % 2 == 1,
                _ => false
            };

            var resolvedLayers = new List<ResolvedLayerAsset>();
            foreach (var layer in preset.Layers.Where(layer => layer.Enabled))
            {
                var color = PickColor(layer.TextColor, i, layer.Pick == FrameLayerPick.Random, baseSeed + i);
                if (layer.Kind == FrameLayerKind.MainVideo)
                {
                    resolvedLayers.Add(new ResolvedLayerAsset(layer, driver, color));
                    continue;
                }

                if (layer.Kind == FrameLayerKind.StaticText)
                {
                    resolvedLayers.Add(new ResolvedLayerAsset(layer, null, color));
                    continue;
                }

                layerPools.TryGetValue(layer.Id, out var pool);
                var chosen = PickAsset(layer, pool ?? [], driver, i, baseSeed);
                resolvedLayers.Add(new ResolvedLayerAsset(layer, chosen, color));
            }

            var tag = resolvedLayers
                .FirstOrDefault(item => item.Layer.Kind is FrameLayerKind.FrameOverlay or FrameLayerKind.ImageOverlay && !string.IsNullOrWhiteSpace(item.AssetPath))
                ?.AssetPath;
            var outputName = string.IsNullOrWhiteSpace(tag)
                ? CleanStem(Path.GetFileNameWithoutExtension(driver)) + ".mp4"
                : FramePlan.OutputName(driver, tag);

            usedNames.TryGetValue(outputName, out var count);
            count++;
            usedNames[outputName] = count;
            if (count > 1)
            {
                outputName = Path.GetFileNameWithoutExtension(outputName) + "_" + count + ".mp4";
            }

            results.Add(new ResolvedTemplateJob(driver, outputName, mirroredLeft, resolvedLayers));
        }

        return results;
    }

    public static string? PickAsset(
        FrameTemplateLayer layer,
        IReadOnlyList<string> pool,
        string driverPath,
        int index,
        int seed)
    {
        if (layer.Pick == FrameLayerPick.Fixed && !string.IsNullOrWhiteSpace(layer.FixedFile) && File.Exists(layer.FixedFile))
        {
            return Path.GetFullPath(layer.FixedFile);
        }

        if (pool.Count == 0)
        {
            return null;
        }

        return layer.Pick switch
        {
            FrameLayerPick.Fixed => pool[0],
            FrameLayerPick.MatchByNumber => MatchByNumberOrStem(driverPath, pool),
            FrameLayerPick.Random => PickShuffledCycle(pool, index, seed),
            _ => pool[index % pool.Count]
        };
    }

    public static string? MatchByNumberOrStem(string driverPath, IReadOnlyList<string> candidates)
    {
        var driverFile = Path.GetFileName(driverPath);
        var exact = candidates.FirstOrDefault(path => SubtitleNameMatcher.IsMatch(driverFile, Path.GetFileName(path)));
        if (exact is not null)
        {
            return exact;
        }

        var driverNum = LeadingNumber(driverFile);
        if (driverNum is not null)
        {
            var byNumber = candidates.FirstOrDefault(path => LeadingNumber(Path.GetFileName(path)) == driverNum);
            if (byNumber is not null)
            {
                return byNumber;
            }
        }

        return null;
    }

    public static int? LeadingNumber(string fileName)
    {
        var match = LeadingNumberRegex.Match(fileName);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num)
            ? num
            : null;
    }

    public static string PickColor(string rawColors, int index, bool random, int seed)
    {
        var parts = (rawColors ?? "#FFFFFF")
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .ToArray();
        if (parts.Length == 0)
        {
            return "#FFFFFF";
        }

        if (parts.Length == 1)
        {
            return parts[0];
        }

        if (random)
        {
            var rng = new Random(seed);
            return parts[rng.Next(parts.Length)];
        }

        return parts[index % parts.Length];
    }

    private static string PickShuffledCycle(IReadOnlyList<string> pool, int index, int seed)
    {
        if (pool.Count == 1)
        {
            return pool[0];
        }

        var cycle = index / pool.Count;
        var offset = index % pool.Count;
        if (cycle == 0)
        {
            return pool[offset];
        }

        var copy = pool.ToList();
        Shuffle(copy, seed + cycle * 313);
        return copy[offset];
    }

    private static string CleanStem(string stem)
    {
        var cleaned = new string(stem.Select(ch => "<>:\"/\\|?*".Contains(ch) ? '_' : ch).ToArray()).Trim();
        return cleaned.Length == 0 ? "clip" : cleaned;
    }

    private static void Shuffle<T>(IList<T> items, int seed)
    {
        var random = new Random(seed);
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swap = random.Next(i + 1);
            (items[i], items[swap]) = (items[swap], items[i]);
        }
    }
}

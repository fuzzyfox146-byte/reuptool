namespace VideoAutoTool.Core.Templates;

public sealed class Template
{
    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "default";

    public CanvasSettings Canvas { get; set; } = new();

    public OutputSettings Output { get; set; } = new();

    public DriverSettings Driver { get; set; } = new();

    public AvatarSideMode AvatarSide { get; set; } = AvatarSideMode.Right;

    public List<LayerDefinition> Layers { get; set; } = [];

    public List<StylePreset> StylePresets { get; set; } = [];
}

public sealed class CanvasSettings
{
    public int Width { get; set; } = 1280;

    public int Height { get; set; } = 720;

    public int Fps { get; set; } = 25;
}

public sealed class OutputSettings
{
    public string Folder { get; set; } = "xuat_render";

    public string NamePattern { get; set; } = "{sourceStem}";

    public OutputEncoder Encoder { get; set; } = OutputEncoder.Auto;

    public int Quality { get; set; } = 23;

    public int AudioBitrateKbps { get; set; } = 192;

    public bool SkipExisting { get; set; } = true;
}

public sealed class DriverSettings
{
    public string Folder { get; set; } = @"source\NGUON";

    public List<string> Extensions { get; set; } = [".mp4", ".mov", ".mkv", ".mp3", ".wav", ".m4a"];

    public string NumberPattern { get; set; } = @"^\s*(\d+)";
}

public sealed class LayerDefinition
{
    public string Id { get; set; } = string.Empty;

    public LayerType Type { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Visible { get; set; } = true;

    public bool Locked { get; set; }

    public bool Frozen { get; set; }

    public SourceSettings? Source { get; set; }

    public ScaleMode ScaleMode { get; set; } = ScaleMode.Cover;

    public double Scale { get; set; } = 1.0;

    public double OffsetX { get; set; }

    public double OffsetY { get; set; }

    public double Opacity { get; set; } = 1.0;

    public TransformSettings? Transform { get; set; }

    public AlphaMode Alpha { get; set; } = AlphaMode.Auto;

    public double BlackKeyTolerance { get; set; } = 0.15;

    public bool MirrorWithSide { get; set; }

    public string Wrap { get; set; } = "pixelWidth";

    public bool StripEmoji { get; set; } = true;

    public bool Upper { get; set; }

    public StyleAssignment? StyleAssignment { get; set; }

    public int? Width { get; set; }
}

public sealed class SourceSettings
{
    public string Folder { get; set; } = string.Empty;

    public List<string> Extensions { get; set; } = [];

    public PickMode Pick { get; set; }

    public string? File { get; set; }

    public int? Seed { get; set; }
}

public sealed class TransformSettings
{
    public Anchor Anchor { get; set; } = Anchor.TopLeft;

    public double X { get; set; }

    public double Y { get; set; }

    public ScaleMode ScaleMode { get; set; } = ScaleMode.Native;

    public double Scale { get; set; } = 1.0;

    public int? Width { get; set; }
}

public sealed class StyleAssignment
{
    public StyleAssignmentMode Mode { get; set; } = StyleAssignmentMode.Fixed;

    public List<string> Presets { get; set; } = [];

    public List<StyleRangeAssignment>? Ranges { get; set; }
}

public sealed class StyleRangeAssignment
{
    public int From { get; set; }

    public int To { get; set; }

    public string Preset { get; set; } = string.Empty;
}

public sealed class StylePreset
{
    public string Id { get; set; } = string.Empty;

    public string Font { get; set; } = "Arial";

    public FontSource FontSource { get; set; } = FontSource.System;

    public int Size { get; set; } = 55;

    public bool Bold { get; set; } = true;

    public string Color { get; set; } = "#FFFFFF";

    public string OutlineColor { get; set; } = "#000000";

    public double Outline { get; set; } = 2.5;

    public double Shadow { get; set; }

    public double LineSpacing { get; set; }

    public SubtitleBox Box { get; set; } = new();

    public TextAlign TextAlign { get; set; } = TextAlign.Center;

    public VerticalAlign VerticalAlign { get; set; } = VerticalAlign.Middle;
}

public sealed class SubtitleBox
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; } = 480;

    public double Height { get; set; } = 220;

    public Anchor Anchor { get; set; } = Anchor.TopLeft;
}

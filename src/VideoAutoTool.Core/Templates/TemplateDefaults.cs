namespace VideoAutoTool.Core.Templates;

public static class TemplateDefaults
{
    public static Template CreateCo139() => new()
    {
        SchemaVersion = TemplateStore.CurrentSchemaVersion,
        Name = "CO139-default",
        Canvas = new CanvasSettings { Width = 1280, Height = 720, Fps = 25 },
        Output = new OutputSettings
        {
            Folder = "xuat_render",
            NamePattern = "{sourceStem}",
            Encoder = OutputEncoder.Auto,
            Quality = 23,
            AudioBitrateKbps = 192,
            SkipExisting = true
        },
        Driver = new DriverSettings
        {
            Folder = @"source\NGUON",
            Extensions = [".mp4", ".mov", ".mkv", ".mp3", ".wav", ".m4a"],
            NumberPattern = @"^\s*(\d+)"
        },
        AvatarSide = AvatarSideMode.Right,
        Layers =
        [
            new LayerDefinition
            {
                Id = "bg",
                Type = LayerType.BackgroundChain,
                Name = "Background",
                Visible = true,
                Frozen = true,
                Source = new SourceSettings
                {
                    Folder = "background",
                    Extensions = [".mp4", ".mov", ".mkv"],
                    Pick = PickMode.SequentialChain
                },
                ScaleMode = ScaleMode.Cover,
                Scale = 1.5,
                Opacity = 0.6
            },
            new LayerDefinition
            {
                Id = "avatar",
                Type = LayerType.Image,
                Name = "Avatar",
                Visible = true,
                Frozen = true,
                Source = new SourceSettings
                {
                    Folder = "avatar",
                    Extensions = [".png", ".webp"],
                    Pick = PickMode.RoundRobin
                },
                Transform = new TransformSettings
                {
                    Anchor = Anchor.BottomRight,
                    X = 1280,
                    Y = 720,
                    ScaleMode = ScaleMode.FitHeight,
                    Scale = 1.0
                },
                MirrorWithSide = true
            },
            new LayerDefinition
            {
                Id = "wave",
                Type = LayerType.LoopVideo,
                Name = "Soundwave",
                Visible = true,
                Frozen = true,
                Source = new SourceSettings
                {
                    Folder = "soundwave",
                    Extensions = [".mov", ".mp4", ".webm"],
                    Pick = PickMode.RoundRobin
                },
                Transform = new TransformSettings
                {
                    Anchor = Anchor.Center,
                    X = 505,
                    Y = 320,
                    ScaleMode = ScaleMode.FitWidth,
                    Width = 500
                },
                Alpha = AlphaMode.Auto,
                BlackKeyTolerance = 0.15,
                MirrorWithSide = true
            },
            new LayerDefinition
            {
                Id = "sub",
                Type = LayerType.Subtitle,
                Name = "Sub",
                Visible = true,
                Source = new SourceSettings
                {
                    Folder = @"text\SUB",
                    Extensions = [".srt"],
                    Pick = PickMode.MatchByNumber
                },
                StyleAssignment = new StyleAssignment
                {
                    Mode = StyleAssignmentMode.Fixed,
                    Presets = ["gold-serif"]
                },
                MirrorWithSide = true
            }
        ],
        StylePresets =
        [
            new StylePreset
            {
                Id = "gold-serif",
                Font = "Georgia",
                FontSource = FontSource.Bundled,
                Size = 55,
                Bold = true,
                Color = "#F5A623",
                OutlineColor = "#000000",
                Outline = 2.5,
                Shadow = 1.0,
                Box = new SubtitleBox { X = 175, Y = 465, Width = 480, Height = 220, Anchor = Anchor.TopLeft },
                TextAlign = TextAlign.Center,
                VerticalAlign = VerticalAlign.Middle
            },
            new StylePreset
            {
                Id = "white-slab",
                Font = "Rockwell Condensed",
                FontSource = FontSource.Bundled,
                Size = 55,
                Bold = true,
                Color = "#FFFFFF",
                OutlineColor = "#000000",
                Outline = 2.5,
                Shadow = 0.0,
                Box = new SubtitleBox { X = 175, Y = 330, Width = 480, Height = 220, Anchor = Anchor.TopLeft },
                TextAlign = TextAlign.Center,
                VerticalAlign = VerticalAlign.Middle
            }
        ]
    };
}

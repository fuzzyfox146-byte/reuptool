using System.Text.Json.Serialization;

namespace VideoAutoTool.Core.Templates;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LayerType
{
    BackgroundChain,
    Image,
    LoopVideo,
    Subtitle,
    FixedText,
    StaticImage
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PickMode
{
    SequentialChain,
    RoundRobin,
    Fixed,
    Random,
    MatchByNumber
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Anchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScaleMode
{
    Cover,
    FitWidth,
    FitHeight,
    Native,
    Stretch
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlphaMode
{
    Auto,
    Alpha,
    BlackKey
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StyleAssignmentMode
{
    Fixed,
    RoundRobin,
    ByRange
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AvatarSideMode
{
    Right,
    Left,
    Alternate
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FontSource
{
    Bundled,
    Imported,
    System
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TextAlign
{
    Left,
    Center,
    Right
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VerticalAlign
{
    Top,
    Middle,
    Bottom
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutputEncoder
{
    Auto,
    Nvenc,
    X264
}

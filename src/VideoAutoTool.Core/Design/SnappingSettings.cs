namespace VideoAutoTool.Core.Design;

/// <summary>
/// Settings for snapping behavior when moving layers.
/// </summary>
public sealed class SnappingSettings
{
    /// <summary>
    /// Whether snapping is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Snap threshold in pixels.
    /// </summary>
    public int Threshold { get; set; } = 10;

    /// <summary>
    /// Snap to canvas center.
    /// </summary>
    public bool SnapToCenter { get; set; } = true;

    /// <summary>
    /// Snap to canvas thirds (rule of thirds).
    /// </summary>
    public bool SnapToThirds { get; set; } = true;

    /// <summary>
    /// Snap to canvas edges.
    /// </summary>
    public bool SnapToEdges { get; set; } = true;

    /// <summary>
    /// Snap to other layers.
    /// </summary>
    public bool SnapToOtherLayers { get; set; } = true;
}

/// <summary>
/// Represents a snap guide line.
/// </summary>
public sealed record SnapGuide(int Position, SnapGuideType Type, string Description);

public enum SnapGuideType
{
    CenterX,
    CenterY,
    ThirdX,
    ThirdY,
    EdgeLeft,
    EdgeRight,
    EdgeTop,
    EdgeBottom,
    Layer
}

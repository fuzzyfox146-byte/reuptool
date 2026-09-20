using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Design;

/// <summary>
/// Provides snapping logic for layer positioning.
/// </summary>
public sealed class SnappingEngine
{
    private readonly int _canvasWidth;
    private readonly int _canvasHeight;

    public SnappingEngine(int canvasWidth, int canvasHeight)
    {
        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;
    }

    /// <summary>
    /// Snaps the given position to nearby guides and returns the snapped position and active guides.
    /// </summary>
    /// <param name="x">Layer's X position.</param>
    /// <param name="y">Layer's Y position.</param>
    /// <param name="layerWidth">Layer's width (for calculating center/right snap points).</param>
    /// <param name="layerHeight">Layer's height (for calculating center/bottom snap points).</param>
    /// <param name="settings">Snapping settings.</param>
    /// <param name="otherLayers">Other layers to snap to (not currently used).</param>
    public (int SnappedX, int SnappedY, List<SnapGuide> ActiveGuides) Snap(
        double x,
        double y,
        int layerWidth,
        int layerHeight,
        SnappingSettings settings,
        IEnumerable<LayerDefinition>? otherLayers = null)
    {
        if (!settings.Enabled)
        {
            return ((int)x, (int)y, new List<SnapGuide>());
        }

        var guides = BuildGuides(settings, otherLayers);
        var activeGuides = new List<SnapGuide>();

        int snappedX = (int)x;
        int snappedY = (int)y;

        // Calculate layer key points (left, center, right for X; top, center, bottom for Y)
        var layerPointsX = new[] { (int)x, (int)x + layerWidth / 2, (int)x + layerWidth };
        var layerPointsY = new[] { (int)y, (int)y + layerHeight / 2, (int)y + layerHeight };

        // Snap X
        int? snapDeltaX = null;
        foreach (var guide in guides.Where(g => IsVerticalGuide(g.Type)))
        {
            foreach (var pointX in layerPointsX)
            {
                int delta = guide.Position - pointX;
                if (Math.Abs(delta) <= settings.Threshold)
                {
                    if (snapDeltaX == null || Math.Abs(delta) < Math.Abs(snapDeltaX.Value))
                    {
                        snapDeltaX = delta;
                        activeGuides.RemoveAll(g => IsVerticalGuide(g.Type));
                        activeGuides.Add(guide);
                    }
                }
            }
        }

        if (snapDeltaX.HasValue)
        {
            snappedX = (int)x + snapDeltaX.Value;
        }

        // Snap Y
        int? snapDeltaY = null;
        foreach (var guide in guides.Where(g => IsHorizontalGuide(g.Type)))
        {
            foreach (var pointY in layerPointsY)
            {
                int delta = guide.Position - pointY;
                if (Math.Abs(delta) <= settings.Threshold)
                {
                    if (snapDeltaY == null || Math.Abs(delta) < Math.Abs(snapDeltaY.Value))
                    {
                        snapDeltaY = delta;
                        activeGuides.RemoveAll(g => IsHorizontalGuide(g.Type));
                        activeGuides.Add(guide);
                    }
                }
            }
        }

        if (snapDeltaY.HasValue)
        {
            snappedY = (int)y + snapDeltaY.Value;
        }

        return (snappedX, snappedY, activeGuides);
    }

    private List<SnapGuide> BuildGuides(SnappingSettings settings, IEnumerable<LayerDefinition>? otherLayers)
    {
        var guides = new List<SnapGuide>();

        if (settings.SnapToCenter)
        {
            guides.Add(new SnapGuide(_canvasWidth / 2, SnapGuideType.CenterX, "Center X"));
            guides.Add(new SnapGuide(_canvasHeight / 2, SnapGuideType.CenterY, "Center Y"));
        }

        if (settings.SnapToThirds)
        {
            guides.Add(new SnapGuide(_canvasWidth / 3, SnapGuideType.ThirdX, "1/3 X"));
            guides.Add(new SnapGuide(_canvasWidth * 2 / 3, SnapGuideType.ThirdX, "2/3 X"));
            guides.Add(new SnapGuide(_canvasHeight / 3, SnapGuideType.ThirdY, "1/3 Y"));
            guides.Add(new SnapGuide(_canvasHeight * 2 / 3, SnapGuideType.ThirdY, "2/3 Y"));
        }

        if (settings.SnapToEdges)
        {
            guides.Add(new SnapGuide(0, SnapGuideType.EdgeLeft, "Left edge"));
            guides.Add(new SnapGuide(_canvasWidth, SnapGuideType.EdgeRight, "Right edge"));
            guides.Add(new SnapGuide(0, SnapGuideType.EdgeTop, "Top edge"));
            guides.Add(new SnapGuide(_canvasHeight, SnapGuideType.EdgeBottom, "Bottom edge"));
        }

        // Note: Snap to other layers requires knowing actual layer dimensions from media probe,
        // which is complex. For now, only canvas guides are supported.
        // Future enhancement: pass layer bounds explicitly.

        return guides;
    }

    private static bool IsVerticalGuide(SnapGuideType type)
    {
        return type is SnapGuideType.CenterX or SnapGuideType.ThirdX or SnapGuideType.EdgeLeft or SnapGuideType.EdgeRight
            or SnapGuideType.Layer && true; // Simplified; in real impl, layer guides need X/Y distinction
    }

    private static bool IsHorizontalGuide(SnapGuideType type)
    {
        return type is SnapGuideType.CenterY or SnapGuideType.ThirdY or SnapGuideType.EdgeTop or SnapGuideType.EdgeBottom;
    }
}

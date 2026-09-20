using VideoAutoTool.Core.Design;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Design;

public sealed class SnappingEngineTests
{
    [Fact]
    public void Snap_DisabledSettings_ReturnsOriginalPosition()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Enabled = false };

        var (x, y, guides) = engine.Snap(100, 200, 50, 50, settings);

        Assert.Equal(100, x);
        Assert.Equal(200, y);
        Assert.Empty(guides);
    }

    [Fact]
    public void Snap_ToCenterX_SnapsCorrectly()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 10 };

        // Layer center at 640 ± threshold should snap to canvas center (640)
        var (x, _, guides) = engine.Snap(630, 100, 20, 20, settings); // layer center at 640

        Assert.Equal(630, x); // layer left stays at 630 so center is at 640
        Assert.Contains(guides, g => g.Type == SnapGuideType.CenterX);
    }

    [Fact]
    public void Snap_ToCenterY_SnapsCorrectly()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 10 };

        // Layer center at 360 ± threshold should snap to canvas center (360)
        var (_, y, guides) = engine.Snap(100, 350, 20, 20, settings); // layer center at 360

        Assert.Equal(350, y); // layer top stays at 350 so center is at 360
        Assert.Contains(guides, g => g.Type == SnapGuideType.CenterY);
    }

    [Fact]
    public void Snap_ToEdgeLeft_SnapsCorrectly()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 10 };

        var (x, _, guides) = engine.Snap(5, 100, 50, 50, settings); // within threshold of 0

        Assert.Equal(0, x);
        Assert.Contains(guides, g => g.Type == SnapGuideType.EdgeLeft);
    }

    [Fact]
    public void Snap_ToEdgeRight_SnapsCorrectly()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 10 };

        // Layer right edge at 1280 (left = 1280 - 50 = 1230)
        var (x, _, guides) = engine.Snap(1235, 100, 50, 50, settings);

        Assert.Equal(1230, x); // snaps so right edge is at 1280
        Assert.Contains(guides, g => g.Type == SnapGuideType.EdgeRight);
    }

    [Fact]
    public void Snap_ToThirds_SnapsCorrectly()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 10 };

        // 1/3 of 1280 = 426.67, snap layer center there
        var (x, _, guides) = engine.Snap(420, 100, 20, 20, settings); // layer center at 430

        Assert.InRange(x, 416, 427); // snapped near 1/3
        Assert.Contains(guides, g => g.Type == SnapGuideType.ThirdX);
    }

    [Fact]
    public void Snap_ToOtherLayer_NotCurrentlySupported()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 10, SnapToOtherLayers = true };

        var otherLayer = new LayerDefinition
        {
            Type = LayerType.Image,
            Transform = new TransformSettings { X = 200, Y = 300, Width = 100 }
        };

        // Snap to other layers not currently supported (requires actual media dimensions)
        var (x, _, guides) = engine.Snap(195, 100, 50, 50, settings, new[] { otherLayer });

        Assert.Equal(195, x); // no snap
        Assert.DoesNotContain(guides, g => g.Type == SnapGuideType.Layer);
    }

    [Fact]
    public void Snap_OutsideThreshold_NoSnap()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 5 };

        var (x, y, guides) = engine.Snap(100, 200, 50, 50, settings);

        Assert.Equal(100, x);
        Assert.Equal(200, y);
        Assert.Empty(guides);
    }

    [Fact]
    public void Snap_PicksClosestGuide_WhenMultipleMatch()
    {
        var engine = new SnappingEngine(1280, 720);
        var settings = new SnappingSettings { Threshold = 50 };

        // Position that could snap to multiple guides - pick closest
        var (x, _, guides) = engine.Snap(10, 100, 50, 50, settings);

        Assert.Equal(0, x); // should snap to edge (0), closest guide
        Assert.Single(guides);
    }
}

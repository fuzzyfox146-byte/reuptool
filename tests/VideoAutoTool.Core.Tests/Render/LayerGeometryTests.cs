using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Render;

public class LayerGeometryTests
{
    [Fact]
    public void TryGetCanvasBox_UsesTopLeftAndMirrorsOnLeftSide()
    {
        var transform = new TransformSettings { X = 720, Y = 0, Width = 560, Height = 720 };
        Assert.True(LayerGeometry.TryGetCanvasBox(transform, 1280, 720, "left", true, out var box));
        Assert.Equal(0, box.Left);
        Assert.Equal(0, box.Top);
        Assert.Equal(560, box.Width);
        Assert.Equal(720, box.Height);
    }

    [Fact]
    public void ResolveFittedOverlay_FitsImageInsideDesignBox()
    {
        var transform = new TransformSettings { X = 0, Y = 0, Width = 1280, Height = 720, Anchor = Anchor.BottomRight };
        var rect = LayerGeometry.ResolveFittedOverlay(
            transform, 400, 800, 1280, 720, "right", true, Anchor.BottomRight, 0, 0, 360, 720);
        Assert.True(rect.Left >= 0);
        Assert.True(rect.Top >= 0);
        Assert.Equal(360, rect.Width);
        Assert.Equal(720, rect.Height);
        Assert.Equal((1280 - 360) / 2, rect.Left);
    }

    [Fact]
    public void SubtitleAnchorPoint_CentersInTopLeftBox()
    {
        var box = new SubtitleBox { X = 175, Y = 465, Width = 480, Height = 220, Anchor = Anchor.TopLeft };
        var (px, py, an) = LayerGeometry.SubtitleAnchorPoint(box, TextAlign.Center, VerticalAlign.Middle, "right", true, 1280);
        Assert.Equal(415, px);
        Assert.Equal(575, py);
        Assert.Equal(5, an);
    }
}

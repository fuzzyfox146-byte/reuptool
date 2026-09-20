using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Render;

public sealed record LayerRect(int Left, int Top, int Width, int Height);

public static class LayerGeometry
{
    public static LayerRect ComputeOverlayRect(
        Anchor anchor,
        double x,
        double y,
        int layerWidth,
        int layerHeight,
        int canvasWidth,
        int canvasHeight,
        string side,
        bool mirrorWithSide)
    {
        if (mirrorWithSide && side.Equals("left", StringComparison.OrdinalIgnoreCase))
        {
            x = canvasWidth - x;
            anchor = MirrorAnchorHorizontal(anchor);
        }

        var (ax, ay) = AnchorFactors(anchor);
        var left = (int)Math.Round(x - layerWidth * ax);
        var top = (int)Math.Round(y - layerHeight * ay);
        return new LayerRect(left, top, layerWidth, layerHeight);
    }

    public static (int Width, int Height) ScaleToFitHeight(double scale, int canvasHeight, int sourceWidth, int sourceHeight)
    {
        var height = MakeEven((int)Math.Round(scale * canvasHeight));
        var width = MakeEven(sourceHeight == 0 ? height : (int)Math.Round(height * (sourceWidth / (double)sourceHeight)));
        return (width, height);
    }

    public static (int Width, int Height) ScaleToFitWidth(int targetWidth, int sourceWidth, int sourceHeight)
    {
        var width = MakeEven(targetWidth);
        var height = MakeEven(sourceWidth == 0 ? width : (int)Math.Round(width * (sourceHeight / (double)sourceWidth)));
        return (width, height);
    }

    public static int MakeEven(int value) => value % 2 == 0 ? value : value + 1;

    private static Anchor MirrorAnchorHorizontal(Anchor anchor) => anchor switch
    {
        Anchor.TopLeft => Anchor.TopRight,
        Anchor.TopRight => Anchor.TopLeft,
        Anchor.Left => Anchor.Right,
        Anchor.Right => Anchor.Left,
        Anchor.BottomLeft => Anchor.BottomRight,
        Anchor.BottomRight => Anchor.BottomLeft,
        _ => anchor
    };

    private static (double Ax, double Ay) AnchorFactors(Anchor anchor) => anchor switch
    {
        Anchor.TopLeft => (0, 0),
        Anchor.Top => (0.5, 0),
        Anchor.TopRight => (1, 0),
        Anchor.Left => (0, 0.5),
        Anchor.Center => (0.5, 0.5),
        Anchor.Right => (1, 0.5),
        Anchor.BottomLeft => (0, 1),
        Anchor.Bottom => (0.5, 1),
        Anchor.BottomRight => (1, 1),
        _ => (0.5, 0.5)
    };

    public static (int X, int Y, int An) SubtitleAnchorPoint(SubtitleBox box, TextAlign textAlign, VerticalAlign verticalAlign, string side, bool mirrorWithSide, int canvasWidth)
    {
        var x = box.X;
        var y = box.Y;
        var anchor = box.Anchor;
        if (mirrorWithSide && side.Equals("left", StringComparison.OrdinalIgnoreCase))
        {
            x = canvasWidth - x;
            anchor = MirrorAnchorHorizontal(anchor);
        }

        var (ax, ay) = AnchorFactors(anchor);
        var px = (int)Math.Round(x + box.Width * ax);
        var py = (int)Math.Round(y + box.Height * ay);
        var an = AssAlignment(textAlign, verticalAlign);
        return (px, py, an);
    }

    private static int AssAlignment(TextAlign textAlign, VerticalAlign verticalAlign) =>
        (verticalAlign, textAlign) switch
        {
            (VerticalAlign.Bottom, TextAlign.Left) => 1,
            (VerticalAlign.Bottom, TextAlign.Center) => 2,
            (VerticalAlign.Bottom, TextAlign.Right) => 3,
            (VerticalAlign.Middle, TextAlign.Left) => 4,
            (VerticalAlign.Middle, TextAlign.Center) => 5,
            (VerticalAlign.Middle, TextAlign.Right) => 6,
            (VerticalAlign.Top, TextAlign.Left) => 7,
            (VerticalAlign.Top, TextAlign.Center) => 8,
            (VerticalAlign.Top, TextAlign.Right) => 9,
            _ => 5
        };
}

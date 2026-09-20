using System.Security.Cryptography;
using System.Text;

namespace VideoAutoTool.Core.Cache;

public sealed class CacheKey
{
    public string FilePath { get; }
    public long FileSize { get; }
    public DateTime LastModified { get; }
    public int CanvasWidth { get; }
    public int CanvasHeight { get; }
    public int CanvasFps { get; }
    public double Scale { get; }
    public double Opacity { get; }
    public string ScaleMode { get; }
    public Dictionary<string, string> ExtraParams { get; }

    public CacheKey(
        string filePath,
        long fileSize,
        DateTime lastModified,
        int canvasWidth,
        int canvasHeight,
        int canvasFps,
        double scale,
        double opacity,
        string scaleMode,
        Dictionary<string, string>? extraParams = null)
    {
        FilePath = filePath;
        FileSize = fileSize;
        LastModified = lastModified;
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        CanvasFps = canvasFps;
        Scale = scale;
        Opacity = opacity;
        ScaleMode = scaleMode;
        ExtraParams = extraParams ?? new Dictionary<string, string>();
    }

    public string ComputeHash()
    {
        var sb = new StringBuilder();
        sb.Append(FilePath);
        sb.Append('|');
        sb.Append(FileSize);
        sb.Append('|');
        sb.Append(LastModified.Ticks);
        sb.Append('|');
        sb.Append(CanvasWidth);
        sb.Append('|');
        sb.Append(CanvasHeight);
        sb.Append('|');
        sb.Append(CanvasFps);
        sb.Append('|');
        sb.Append(Scale.ToString("F6"));
        sb.Append('|');
        sb.Append(Opacity.ToString("F6"));
        sb.Append('|');
        sb.Append(ScaleMode);

        foreach (var kvp in ExtraParams.OrderBy(x => x.Key))
        {
            sb.Append('|');
            sb.Append(kvp.Key);
            sb.Append('=');
            sb.Append(kvp.Value);
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

namespace VideoAutoTool.Core.Render;

public enum RenderMode
{
    Full,
    Clip,
    Frame
}

public sealed record RenderRequest(RenderMode Mode, double? ClipSeconds = null, double? FrameTimeSeconds = null);

namespace FrameRender;

/// <summary>
/// The fast publish uses its own session folder so it can run beside the current FrameRender.
/// </summary>
internal static class FrameEdition
{
#if FRAME_FAST_EDITION
    public const string DataFolder = "FrameRender-2";
    public const string Title = "Frame Render 2";
    public const bool PreferGpu = true;
    public const bool AutoSiblingOutput = true;
#else
    public const string DataFolder = "FrameRender";
    public const string Title = "Frame Render — hàng đợi";
    public const bool PreferGpu = false;
    public const bool AutoSiblingOutput = false;
#endif

    public static string ErrorLog(string outputPath) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DataFolder,
            "logs",
            Path.GetFileNameWithoutExtension(outputPath) + ".log");
}

namespace VideoAutoTool.App.Services;

/// <summary>
/// GPU test build uses its own session folder so it can run beside publish-1.0.25.
/// </summary>
internal static class ProductEdition
{
#if VAT_OPT_EDITION
    public const string DataFolder = "VideoAutoTool-Opt";
    public const string Title = "Video Auto Tool 2.1.1 Opt";
    public const bool IsGpuEdition = false;
    public const bool IsOptEdition = true;
#elif VAT_GPU_EDITION
    public const string DataFolder = "VideoAutoTool-Gpu";
    public const string Title = "Video Auto Tool 2.0.0 GPU";
    public const bool IsGpuEdition = true;
    public const bool IsOptEdition = false;
#else
    public const string DataFolder = "VideoAutoTool";
    public const string Title = "Video Auto Tool";
    public const bool IsGpuEdition = false;
    public const bool IsOptEdition = false;
#endif
}

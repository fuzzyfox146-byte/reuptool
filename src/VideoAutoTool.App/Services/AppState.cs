using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.Services;

public sealed class AppState
{
    public Template Template { get; set; } = TemplateDefaults.CreateCo139();

    public string? RootPath { get; set; }

    public string? TemplatePath { get; set; }

    public bool IsDirty { get; set; }

    public string? FfmpegDirectory { get; set; }

    public string SelectedStylePresetId { get; set; } = "gold-serif";
}

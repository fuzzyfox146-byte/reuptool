namespace VideoAutoTool.App.ViewModels;

public sealed class DesignChoice
{
    public DesignChoice(string? path, string displayName)
    {
        Path = path;
        DisplayName = displayName;
    }

    public string? Path { get; }

    public string DisplayName { get; }

    public bool IsCurrent => Path is null;
}

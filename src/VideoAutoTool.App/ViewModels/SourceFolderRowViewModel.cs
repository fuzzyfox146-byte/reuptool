using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.Core.Scanning;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class SourceFolderRowViewModel : ObservableObject
{
    private readonly Action<SourceFolderRowViewModel> _browse;

    public SourceFolderKind Kind { get; }

    public string RoleId { get; }

    public string RoleLabel { get; }

    public bool UsedForRender { get; }

    public string ExtensionsLabel { get; }

    [ObservableProperty]
    private string _folderPath = "";

    [ObservableProperty]
    private string _absolutePath = "";

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private bool _folderExists;

    [ObservableProperty]
    private bool _isReady;

    public string ReadyMark => IsReady ? "✅" : "—";

    public string RenderMark => UsedForRender ? "Render" : "Khác";

    public SourceFolderRowViewModel(
        SourceFolderKind kind,
        string roleId,
        string roleLabel,
        bool usedForRender,
        string extensionsLabel,
        Action<SourceFolderRowViewModel> browse)
    {
        Kind = kind;
        RoleId = roleId;
        RoleLabel = roleLabel;
        UsedForRender = usedForRender;
        ExtensionsLabel = extensionsLabel;
        _browse = browse;
    }

    partial void OnIsReadyChanged(bool value) => OnPropertyChanged(nameof(ReadyMark));

    public void ApplyStatus(SourceFolderStatus status)
    {
        FolderPath = status.Folder;
        AbsolutePath = status.AbsolutePath;
        FileCount = status.FileCount;
        FolderExists = status.FolderExists;
        IsReady = status.IsReady;
    }

    [RelayCommand]
    private void Browse() => _browse(this);
}

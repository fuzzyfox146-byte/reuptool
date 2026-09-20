namespace VideoAutoTool.App.Services;

/// <summary>
/// UI dialogs. Implemented in WPF so ViewModels stay free of Window types.
/// </summary>
public interface IUiDialogs
{
    string? PickFolder(string title, string? initialDirectory = null);

    string? PickFontFile();
}

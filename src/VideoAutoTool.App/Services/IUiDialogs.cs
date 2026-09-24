namespace VideoAutoTool.App.Services;

/// <summary>
/// UI dialogs. Implemented in WPF so ViewModels stay free of Window types.
/// </summary>
public interface IUiDialogs
{
    string? PickFolder(string title, string? initialDirectory = null);

    string? PickFile(string title, string filter, string? initialDirectory = null);

    bool Confirm(string title, string message);

    string? PickFontFile();

    string? PickOpenJson(string title, string? initialDirectory = null);

    string? PickSaveJson(string title, string? initialDirectory = null, string? suggestedFileName = null);

    UnsavedCloseChoice ConfirmUnsavedClose();

    bool ConfirmStopQueueOnExit();

    void ShowError(string title, string message);
}

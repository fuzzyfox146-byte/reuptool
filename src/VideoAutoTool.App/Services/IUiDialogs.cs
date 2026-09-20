namespace VideoAutoTool.App.Services;

/// <summary>
/// UI dialogs. Implemented in WPF so ViewModels stay free of Window types.
/// </summary>
public interface IUiDialogs
{
    string? PickFolder(string title, string? initialDirectory = null);

    string? PickFontFile();

    string? PickOpenJson(string title, string? initialDirectory = null);

    string? PickSaveJson(string title, string? initialDirectory = null, string? suggestedFileName = null);

    UnsavedCloseChoice ConfirmUnsavedClose();

    bool ConfirmStopQueueOnExit();

    void ShowError(string title, string message);
}

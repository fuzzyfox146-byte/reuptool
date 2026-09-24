using System.Windows;
using Microsoft.Win32;

namespace VideoAutoTool.App.Services;

public sealed class WpfUiDialogs : IUiDialogs
{
    public string? PickFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickFile(string title, string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public string? PickFontFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn font (.ttf / .otf)",
            Filter = "Font (*.ttf;*.otf)|*.ttf;*.otf|Tất cả (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOpenJson(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Design JSON (*.json)|*.json|Tất cả (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveJson(string title, string? initialDirectory = null, string? suggestedFileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = "Design JSON (*.json)|*.json",
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = false,
            FileName = suggestedFileName ?? ""
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public UnsavedCloseChoice ConfirmUnsavedClose()
    {
        var result = MessageBox.Show(
            "Bạn chưa lưu thiết kế và cài đặt hiện tại.\n\n• Có = lưu toàn bộ (như Ctrl+S) rồi thoát\n• Không = thoát, bỏ thay đổi chưa lưu\n• Hủy = ở lại cửa sổ",
            "Chưa lưu",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => UnsavedCloseChoice.Save,
            MessageBoxResult.No => UnsavedCloseChoice.Discard,
            _ => UnsavedCloseChoice.Cancel
        };
    }

    public bool ConfirmStopQueueOnExit()
    {
        return MessageBox.Show(
            "Hàng đợi đang render. Thoát sẽ hủy job đang chạy.\nBạn có chắc muốn thoát?",
            "Đang render",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

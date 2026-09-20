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
}

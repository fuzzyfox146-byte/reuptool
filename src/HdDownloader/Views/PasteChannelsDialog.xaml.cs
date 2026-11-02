using System.Windows;
using HdDownloader.Services;
using HdDownloader.ViewModels;

namespace HdDownloader.Views;

public partial class PasteChannelsDialog : Window
{
    private readonly PackDownloadViewModel _viewModel;
    private List<ParsedChannel> _parsed = [];

    public PasteChannelsDialog(PackDownloadViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Loaded += PasteChannelsDialog_Loaded;
        InputBox.TextChanged += (s, e) => UpdateCount();
    }

    private void PasteChannelsDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var clip = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(clip) && (clip.Contains("youtube.com") || clip.Contains("youtu.be")))
                {
                    InputBox.Text = clip;
                    InputBox.SelectAll();
                }
            }
        }
        catch
        {
            // Ignore clipboard access issues
        }

        UpdateCount();
        InputBox.Focus();
    }

    private void UpdateCount()
    {
        _parsed = ChannelListParser.Parse(InputBox.Text, _viewModel.Channels.LastOrDefault()?.FolderName);
        CountText.Text = $"✓ Nhận diện được: {_parsed.Count} kênh hợp lệ";
        ImportBtn.IsEnabled = _parsed.Count > 0;
        ImportBtn.Content = _parsed.Count > 0 ? $"✓ Nhập {_parsed.Count} kênh vào danh sách" : "✓ Nhập vào danh sách";
    }

    private void PasteClipboardBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                InputBox.Text = Clipboard.GetText();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể đọc Clipboard: {ex.Message}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_parsed.Count == 0)
        {
            return;
        }

        var replace = ReplaceCheck.IsChecked == true;
        _viewModel.ImportChannels(_parsed, replace);
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

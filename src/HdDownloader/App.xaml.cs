using System.Windows;
using HdDownloader.ViewModels;
using VideoAutoTool.Core.Download;

namespace HdDownloader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(new PackDownloadViewModel(new PackDownloader(new YtDlpProcessRunner())));
        window.Show();
    }
}

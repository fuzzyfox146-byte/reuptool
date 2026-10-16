using System.Windows;
using HdDownloader.ViewModels;
using VideoAutoTool.Core.Download;

namespace HdDownloader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if HD_VIEW_EDITION
        HdDownloader.Services.PackSettingsStore.AppFolder = "HdDownloaderView";
        HdDownloader.Services.PackSettingsStore.DefaultOrderByViews = true;
        HdDownloader.Services.PackSettingsStore.DefaultDownloadSubtitles = true;
#endif
        var window = new MainWindow(new PackDownloadViewModel(new PackDownloader(new YtDlpProcessRunner())));
        window.Show();
    }
}

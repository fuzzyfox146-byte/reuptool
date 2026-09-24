using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;
using VideoAutoTool.Core.Validation;
using VideoAutoTool.Core.Cache;
using VideoAutoTool.Core.Download;
using VideoAutoTool.App.ViewModels;
using VideoAutoTool.App.Services;

namespace VideoAutoTool.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core services
        FfmpegPaths ffmpegPaths;
        try
        {
            ffmpegPaths = FfmpegLocator.Locate(null);
        }
        catch
        {
            // Fallback if ffmpeg not found
            ffmpegPaths = new FfmpegPaths("ffmpeg", "ffprobe", "unknown");
        }
        
        services.AddSingleton(ffmpegPaths);
        services.AddSingleton<IMediaProbe, FfmpegProbe>();
        services.AddSingleton<FfmpegRunner>();
        services.AddSingleton<FontCatalog>();
        services.AddSingleton<IFontCatalog>(sp => sp.GetRequiredService<FontCatalog>());
        services.AddSingleton<ITextMeasurer, SkiaTextMeasurer>();
        services.AddSingleton<JobPlanner>();
        services.AddSingleton<ValidationEngine>();
        services.AddSingleton<EncoderSelector>();
        services.AddSingleton<CacheStore>();
        services.AddSingleton<PreparedAssetService>();
        services.AddSingleton<JobStore>();

        // Builders
        services.AddTransient<AssBuilder>();
        services.AddSingleton<JobRenderer>();
        services.AddSingleton<JobRendererAdapter>();

        services.AddSingleton<IUiDialogs, WpfUiDialogs>();

        // ViewModels (singletons so Design/Source/Queue share the same session)
        services.AddSingleton<DesignViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<SourceViewModel>();
        services.AddSingleton<IYtDlpRunner, YtDlpProcessRunner>();
        services.AddSingleton<SourceDownloader>();
        services.AddSingleton<MultiChannelDownloader>();
        services.AddSingleton<DownloadViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoTool.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Video Auto Tool";

    [ObservableProperty]
    private int _selectedTabIndex;

    public DesignViewModel DesignViewModel { get; }
    public SourceViewModel SourceViewModel { get; }
    public QueueViewModel QueueViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    public MainViewModel(
        DesignViewModel designViewModel,
        SourceViewModel sourceViewModel,
        QueueViewModel queueViewModel,
        SettingsViewModel settingsViewModel)
    {
        DesignViewModel = designViewModel;
        SourceViewModel = sourceViewModel;
        QueueViewModel = queueViewModel;
        SettingsViewModel = settingsViewModel;
    }
}

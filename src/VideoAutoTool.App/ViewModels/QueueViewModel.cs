using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoTool.App.ViewModels;

public partial class QueueViewModel : ObservableObject
{
    [ObservableProperty]
    private int _jobCount;
}

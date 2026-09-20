using VideoAutoTool.App.ViewModels;

namespace VideoAutoTool.App;

public partial class MainWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

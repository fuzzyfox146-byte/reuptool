using System.ComponentModel;
using System.Windows;
using VideoAutoTool.App.ViewModels;

namespace VideoAutoTool.App;

public partial class MainWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && !vm.RequestClose())
        {
            e.Cancel = true;
        }
    }
}

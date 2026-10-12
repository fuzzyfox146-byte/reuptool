using System.ComponentModel;
using System.Windows;
using HdDownloader.ViewModels;

namespace HdDownloader;

public partial class MainWindow : Window
{
    private readonly PackDownloadViewModel _model;

    public MainWindow(PackDownloadViewModel model)
    {
        _model = model;
        InitializeComponent();
        DataContext = model;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _model.CancelAll();
        _model.Save();
        base.OnClosing(e);
    }
}

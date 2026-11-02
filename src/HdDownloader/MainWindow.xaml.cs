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

        _model.RequestOpenPasteDialog += () =>
        {
            var dialog = new Views.PasteChannelsDialog(_model) { Owner = this };
            dialog.ShowDialog();
        };

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.S && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                // Commit any active editing in focused controls (TextBoxes etc.)
                var focused = System.Windows.Input.Keyboard.FocusedElement as System.Windows.FrameworkElement;
                focused?.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                focused?.Focus();
                _model.SaveExplicit();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.V && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                // If not typing inside an existing TextBox, Ctrl+V opens the bulk paste dialog
                if (System.Windows.Input.Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
                {
                    var dialog = new Views.PasteChannelsDialog(_model) { Owner = this };
                    dialog.ShowDialog();
                    e.Handled = true;
                }
            }
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _model.CancelAll();
        _model.Save();
        base.OnClosing(e);
    }
}

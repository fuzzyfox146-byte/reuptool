using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.App.Services;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IUiDialogs _dialogs;
    private string _savedFingerprint = "";
    private bool _restoring;

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
        SettingsViewModel settingsViewModel,
        IUiDialogs dialogs)
    {
        DesignViewModel = designViewModel;
        SourceViewModel = sourceViewModel;
        QueueViewModel = queueViewModel;
        SettingsViewModel = settingsViewModel;
        _dialogs = dialogs;
        SourceViewModel.GoToQueueRequested += (_, _) => SelectedTabIndex = 2;

        RestoreSession();
        HookDirtyTracking();
        MarkSaved();
    }

    public bool HasUnsavedChanges => SessionStore.Fingerprint(BuildSession()) != _savedFingerprint;

    [RelayCommand]
    private void SaveAll()
    {
        if (!TrySaveAll())
        {
            return;
        }

        DesignViewModel.Status = $"Đã lưu toàn bộ (Ctrl+S): {Path.GetFileName(DesignViewModel.TemplatePath)}";
    }

    public bool RequestClose()
    {
        if (QueueViewModel.IsRunning)
        {
            if (!_dialogs.ConfirmStopQueueOnExit())
            {
                return false;
            }

            if (QueueViewModel.CancelCommand.CanExecute(null))
            {
                QueueViewModel.CancelCommand.Execute(null);
            }
        }

        if (!HasUnsavedChanges)
        {
            return true;
        }

        switch (_dialogs.ConfirmUnsavedClose())
        {
            case UnsavedCloseChoice.Save:
                return TrySaveAll();
            case UnsavedCloseChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    private bool TrySaveAll()
    {
        try
        {
            DesignViewModel.SaveCurrentDesign();
            SessionStore.Save(BuildSession());
            MarkSaved();
            return true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Không lưu được", ex.Message);
            return false;
        }
    }

    private void RestoreSession()
    {
        AppSession? session;
        try
        {
            session = SessionStore.TryLoad();
        }
        catch
        {
            return;
        }

        if (session is null) return;

        _restoring = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(session.DesignPath) && File.Exists(session.DesignPath))
            {
                DesignViewModel.LoadFromFile(session.DesignPath);
            }
            else if (session.Template.Layers.Count > 0)
            {
                DesignViewModel.ApplyTemplate(session.Template, session.DesignPath);
            }

            SourceViewModel.RestoreWorkspace(session.RootFolder, session.SelectedDesignPath, session.FolderOverrides);
            if (session.ParallelCount >= 1)
            {
                SettingsViewModel.ParallelCount = session.ParallelCount;
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    private void HookDirtyTracking()
    {
        DesignViewModel.ContentChanged += (_, _) => RefreshDirtyTitle();
        SourceViewModel.PropertyChanged += (_, _) => RefreshDirtyTitle();
        SourceViewModel.WorkspaceChanged += (_, _) => RefreshDirtyTitle();
        SettingsViewModel.PropertyChanged += (_, _) => RefreshDirtyTitle();
        SourceViewModel.Folders.CollectionChanged += (_, _) => RefreshDirtyTitle();
    }

    private void RefreshDirtyTitle()
    {
        if (_restoring) return;
        Title = HasUnsavedChanges ? "Video Auto Tool *" : "Video Auto Tool";
    }

    private void MarkSaved()
    {
        _savedFingerprint = SessionStore.Fingerprint(BuildSession());
        Title = "Video Auto Tool";
    }

    private AppSession BuildSession() => new()
    {
        Template = TemplateStore.Clone(DesignViewModel.ExportTemplate()),
        DesignPath = Path.IsPathRooted(DesignViewModel.TemplatePath) ? DesignViewModel.TemplatePath : null,
        SelectedDesignPath = SourceViewModel.SelectedDesign?.Path,
        RootFolder = SourceViewModel.RootFolder,
        FolderOverrides = SourceViewModel.CaptureFolderOverrides(),
        FfmpegPath = SettingsViewModel.FfmpegPath,
        ParallelCount = SettingsViewModel.ParallelCount
    };
}

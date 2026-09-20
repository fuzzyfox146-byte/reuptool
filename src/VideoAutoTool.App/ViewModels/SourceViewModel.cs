using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.App.Services;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Templates;
using VideoAutoTool.Core.Validation;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class SourceViewModel : ObservableObject
{
    private const double TestClipSeconds = 8;

    private readonly JobPlanner _planner;
    private readonly ValidationEngine _validator;
    private readonly IUiDialogs _dialogs;
    private readonly DesignViewModel _design;
    private readonly QueueViewModel _queue;
    private readonly SettingsViewModel _settings;
    private Template _workingTemplate;
    private bool _suppressDesignSelection;

    [ObservableProperty]
    private string _rootFolder = "";

    [ObservableProperty]
    private ObservableCollection<SourceFolderRowViewModel> _folders = new();

    [ObservableProperty]
    private ObservableCollection<ValidationIssue> _validationMessages = new();

    [ObservableProperty]
    private ObservableCollection<DesignChoice> _designOptions = new();

    [ObservableProperty]
    private DesignChoice? _selectedDesign;

    [ObservableProperty]
    private string _status = "Chọn thư mục gốc bằng nút Duyệt — không cần gõ đường dẫn.";

    public SourceViewModel(
        JobPlanner planner,
        ValidationEngine validator,
        IUiDialogs dialogs,
        DesignViewModel design,
        QueueViewModel queue,
        SettingsViewModel settings)
    {
        _planner = planner;
        _validator = validator;
        _dialogs = dialogs;
        _design = design;
        _queue = queue;
        _settings = settings;
        _workingTemplate = TemplateStore.Clone(design.ExportTemplate());
        _design.DesignsChanged += (_, _) => ReloadDesignList();
        _queue.BusyChanged += (_, _) => NotifyRenderCommands();
        ReloadDesignList();
    }

    public List<FolderOverride> CaptureFolderOverrides()
    {
        return Folders
            .Select(row => new FolderOverride
            {
                RoleId = row.RoleId,
                Folder = string.IsNullOrWhiteSpace(row.AbsolutePath) ? row.FolderPath : row.AbsolutePath
            })
            .Where(o => !string.IsNullOrWhiteSpace(o.Folder))
            .ToList();
    }

    public void RestoreWorkspace(string? rootFolder, string? selectedDesignPath, IReadOnlyList<FolderOverride> overrides)
    {
        if (!string.IsNullOrWhiteSpace(rootFolder))
        {
            RootFolder = rootFolder;
        }

        _suppressDesignSelection = true;
        DesignOptions.Clear();
        DesignOptions.Add(new DesignChoice(null, "Thiết kế đang mở"));
        foreach (var file in DesignLibrary.List())
        {
            DesignOptions.Add(new DesignChoice(file.Path, file.DisplayName));
        }

        SelectedDesign = DesignOptions.FirstOrDefault(d => d.Path == selectedDesignPath)
            ?? DesignOptions.FirstOrDefault();
        _suppressDesignSelection = false;
        ApplySelectedDesignTemplate();

        foreach (var item in overrides)
        {
            var row = Folders.FirstOrDefault(r => r.RoleId == item.RoleId);
            if (row is null || string.IsNullOrWhiteSpace(item.Folder)) continue;
            ApplyFolderOverride(_workingTemplate, row, item.Folder);
            row.FolderPath = item.Folder;
        }

        RefreshFolderStats();
    }

    public event EventHandler? GoToQueueRequested;

    public event EventHandler? WorkspaceChanged;

    public bool CanStartRender =>
        !_queue.IsRunning
        && !string.IsNullOrWhiteSpace(RootFolder)
        && Directory.Exists(RootFolder)
        && Folders.Where(f => f.UsedForRender).All(f => f.IsReady);

    [RelayCommand]
    private void BrowseRoot()
    {
        var picked = _dialogs.PickFolder("Chọn thư mục gốc dự án", string.IsNullOrWhiteSpace(RootFolder) ? null : RootFolder);
        if (string.IsNullOrWhiteSpace(picked)) return;
        RootFolder = picked;
        RefreshFolderStats();
        Status = $"Đã tải thư mục gốc: {RootFolder}";
    }

    [RelayCommand]
    private void RefreshFolderStats()
    {
        if (string.IsNullOrWhiteSpace(RootFolder) || !Directory.Exists(RootFolder))
        {
            foreach (var row in Folders)
            {
                row.FileCount = 0;
                row.FolderExists = false;
                row.IsReady = false;
            }

            Status = string.IsNullOrWhiteSpace(RootFolder)
                ? "Chưa chọn thư mục gốc."
                : $"Thư mục không tồn tại: {RootFolder}";
            NotifyRenderCommands();
            WorkspaceChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var slots = SourceFolderInventory.FromTemplate(_workingTemplate);
        foreach (var slot in slots)
        {
            var row = Folders.FirstOrDefault(r => r.RoleId == slot.RoleId);
            if (row is null) continue;
            row.ApplyStatus(SourceFolderInventory.Inspect(RootFolder, slot));
        }

        var ready = Folders.Count(f => f.UsedForRender && f.IsReady);
        var total = Folders.Count(f => f.UsedForRender);
        Status = $"Đã quét thư mục (chỉ tầng trên, không đọc nội dung file). Sẵn sàng {ready}/{total} slot render.";
        NotifyRenderCommands();
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ValidateDeepAsync()
    {
        if (string.IsNullOrWhiteSpace(RootFolder) || !Directory.Exists(RootFolder))
        {
            Status = "Chọn thư mục gốc trước khi kiểm tra sâu.";
            return;
        }

        Status = "Đang kiểm tra sâu...";
        ValidationMessages.Clear();

        try
        {
            var template = BuildRenderTemplate();
            var report = await _validator.RunAsync(template, RootFolder, null, CancellationToken.None);
            foreach (var issue in report.Issues)
            {
                ValidationMessages.Add(issue);
            }

            var plan = await _planner.PlanAsync(template, RootFolder, CancellationToken.None);
            Status = $"Kiểm tra xong: {plan.Jobs.Count} job, {report.ErrorCount} lỗi, {report.WarningCount} cảnh báo.";
        }
        catch (Exception ex)
        {
            Status = $"Lỗi: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartRender))]
    private async Task TestRenderAsync()
    {
        await StartRenderAsync(testOnly: true);
    }

    [RelayCommand(CanExecute = nameof(CanStartRender))]
    private async Task RenderAllAsync()
    {
        await StartRenderAsync(testOnly: false);
    }

    public void ReloadDesignList()
    {
        var previous = SelectedDesign?.Path;
        _suppressDesignSelection = true;
        DesignOptions.Clear();
        DesignOptions.Add(new DesignChoice(null, "Thiết kế đang mở"));
        foreach (var file in DesignLibrary.List())
        {
            DesignOptions.Add(new DesignChoice(file.Path, file.DisplayName));
        }

        SelectedDesign = DesignOptions.FirstOrDefault(d => d.Path == previous) ?? DesignOptions[0];
        _suppressDesignSelection = false;
        ApplySelectedDesignTemplate();
    }

    partial void OnSelectedDesignChanged(DesignChoice? value)
    {
        if (_suppressDesignSelection || value is null) return;
        ApplySelectedDesignTemplate();
        RefreshFolderStats();
    }

    private void ApplySelectedDesignTemplate()
    {
        if (SelectedDesign is null || SelectedDesign.IsCurrent)
        {
            _workingTemplate = TemplateStore.Clone(_design.ExportTemplate());
        }
        else
        {
            _workingTemplate = TemplateStore.Load(SelectedDesign.Path!);
        }

        RebuildRows();
    }

    private async Task StartRenderAsync(bool testOnly)
    {
        if (!CanStartRender)
        {
            Status = "Chưa đủ folder render. Quét lại sau khi duyệt đủ slot ✅.";
            return;
        }

        try
        {
            var template = BuildRenderTemplate();
            Status = "Đang lập kế hoạch job…";
            var plan = await _planner.PlanAsync(template, RootFolder, CancellationToken.None);
            if (plan.Jobs.Count == 0)
            {
                Status = "Không có job nào để render (kiểm tra nguồn / nền đủ dài).";
                return;
            }

            RenderRequest request;
            if (testOnly)
            {
                var first = plan.Jobs[0];
                var dir = Path.GetDirectoryName(first.OutputPath) ?? "";
                var stem = Path.GetFileNameWithoutExtension(first.OutputPath);
                var testPath = Path.Combine(dir, $"{stem}_test.mp4");
                plan = new PlanResult([first with { OutputPath = testPath }], plan.Warnings);
                request = new RenderRequest(RenderMode.Clip, ClipSeconds: TestClipSeconds);
                Status = $"Render thử job đầu (~{TestClipSeconds:0}s).";
            }
            else
            {
                request = new RenderRequest(RenderMode.Full);
                Status = $"Render thật {plan.Jobs.Count} job.";
            }

            GoToQueueRequested?.Invoke(this, EventArgs.Empty);
            await _queue.StartAsync(template, plan, request, _settings.ParallelCount);
        }
        catch (Exception ex)
        {
            Status = $"Không chạy được render: {ex.Message}";
        }
        finally
        {
            NotifyRenderCommands();
        }
    }

    private Template BuildRenderTemplate()
    {
        var template = SelectedDesign is null || SelectedDesign.IsCurrent
            ? TemplateStore.Clone(_design.ExportTemplate())
            : TemplateStore.Clone(TemplateStore.Load(SelectedDesign.Path!));

        foreach (var row in Folders)
        {
            ApplyFolderOverride(template, row);
        }

        _workingTemplate = template;
        return template;
    }

    private void RebuildRows()
    {
        Folders.Clear();
        foreach (var slot in SourceFolderInventory.FromTemplate(_workingTemplate))
        {
            var row = new SourceFolderRowViewModel(
                slot.Kind,
                slot.RoleId,
                RoleLabel(slot.Kind),
                slot.UsedForRender,
                slot.Extensions.Count == 0 ? "(thư mục xuất)" : string.Join(" ", slot.Extensions),
                BrowseSlot);
            row.FolderPath = slot.Folder;
            Folders.Add(row);
        }

        if (!string.IsNullOrWhiteSpace(RootFolder) && Directory.Exists(RootFolder))
        {
            RefreshFolderStats();
        }
        else
        {
            NotifyRenderCommands();
        }
    }

    private void BrowseSlot(SourceFolderRowViewModel row)
    {
        var initial = Directory.Exists(row.AbsolutePath) ? row.AbsolutePath : RootFolder;
        var picked = _dialogs.PickFolder($"Chọn thư mục: {row.RoleLabel}", string.IsNullOrWhiteSpace(initial) ? null : initial);
        if (string.IsNullOrWhiteSpace(picked)) return;

        ApplyFolderOverride(_workingTemplate, row, picked);
        row.FolderPath = picked;
        if (string.IsNullOrWhiteSpace(RootFolder))
        {
            RootFolder = Directory.GetParent(picked)?.FullName ?? picked;
        }

        RefreshFolderStats();
        Status = $"Đã gán {row.RoleLabel}: {picked}";
    }

    private static void ApplyFolderOverride(Template template, SourceFolderRowViewModel row, string? absoluteFolder = null)
    {
        var folder = absoluteFolder ?? (string.IsNullOrWhiteSpace(row.AbsolutePath) ? row.FolderPath : row.AbsolutePath);
        if (string.IsNullOrWhiteSpace(folder)) return;

        switch (row.Kind)
        {
            case SourceFolderKind.Driver:
                template.Driver.Folder = folder;
                break;
            case SourceFolderKind.Output:
                template.Output.Folder = folder;
                break;
            default:
                var layer = template.Layers.FirstOrDefault(l => l.Id == row.RoleId);
                if (layer?.Source != null)
                {
                    layer.Source.Folder = folder;
                }
                break;
        }
    }

    private void NotifyRenderCommands()
    {
        OnPropertyChanged(nameof(CanStartRender));
        TestRenderCommand.NotifyCanExecuteChanged();
        RenderAllCommand.NotifyCanExecuteChanged();
    }

    private static string RoleLabel(SourceFolderKind kind) => kind switch
    {
        SourceFolderKind.Driver => "Video nguồn (render)",
        SourceFolderKind.Background => "Nền (render)",
        SourceFolderKind.Avatar => "Avatar (render)",
        SourceFolderKind.Soundwave => "Soundwave (render)",
        SourceFolderKind.Subtitle => "Phụ đề SRT (render)",
        SourceFolderKind.Output => "Thư mục xuất render",
        _ => "Khác"
    };
}

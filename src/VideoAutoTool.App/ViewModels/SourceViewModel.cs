using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.App.Services;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Templates;
using VideoAutoTool.Core.Validation;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class SourceViewModel : ObservableObject
{
    private readonly JobPlanner _planner;
    private readonly ValidationEngine _validator;
    private readonly IUiDialogs _dialogs;
    private readonly Template _template;

    [ObservableProperty]
    private string _rootFolder = "";

    [ObservableProperty]
    private ObservableCollection<SourceFolderRowViewModel> _folders = new();

    [ObservableProperty]
    private ObservableCollection<ValidationIssue> _validationMessages = new();

    [ObservableProperty]
    private string _status = "Chọn thư mục gốc bằng nút Duyệt — không cần gõ đường dẫn.";

    public SourceViewModel(JobPlanner planner, ValidationEngine validator, IUiDialogs dialogs)
    {
        _planner = planner;
        _validator = validator;
        _dialogs = dialogs;
        _template = TemplateDefaults.CreateCo139();
        RebuildRows();
    }

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
            return;
        }

        var slots = SourceFolderInventory.FromTemplate(_template);
        foreach (var slot in slots)
        {
            var row = Folders.FirstOrDefault(r => r.RoleId == slot.RoleId);
            if (row is null) continue;
            row.ApplyStatus(SourceFolderInventory.Inspect(RootFolder, slot));
        }

        var ready = Folders.Count(f => f.UsedForRender && f.IsReady);
        var total = Folders.Count(f => f.UsedForRender);
        Status = $"Đã quét thư mục (chỉ tầng trên, không đọc nội dung file). Sẵn sàng {ready}/{total} slot render.";
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
            var report = await _validator.RunAsync(_template, RootFolder, null, CancellationToken.None);
            foreach (var issue in report.Issues)
            {
                ValidationMessages.Add(issue);
            }

            var plan = await _planner.PlanAsync(_template, RootFolder, CancellationToken.None);
            Status = $"Kiểm tra xong: {plan.Jobs.Count} job, {report.ErrorCount} lỗi, {report.WarningCount} cảnh báo.";
        }
        catch (Exception ex)
        {
            Status = $"Lỗi: {ex.Message}";
        }
    }

    private void RebuildRows()
    {
        Folders.Clear();
        foreach (var slot in SourceFolderInventory.FromTemplate(_template))
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
    }

    private void BrowseSlot(SourceFolderRowViewModel row)
    {
        var initial = Directory.Exists(row.AbsolutePath) ? row.AbsolutePath : RootFolder;
        var picked = _dialogs.PickFolder($"Chọn thư mục: {row.RoleLabel}", string.IsNullOrWhiteSpace(initial) ? null : initial);
        if (string.IsNullOrWhiteSpace(picked)) return;

        ApplyFolderOverride(row, picked);
        if (string.IsNullOrWhiteSpace(RootFolder))
        {
            RootFolder = Directory.GetParent(picked)?.FullName ?? picked;
        }

        RefreshFolderStats();
        Status = $"Đã gán {row.RoleLabel}: {picked}";
    }

    private void ApplyFolderOverride(SourceFolderRowViewModel row, string absoluteFolder)
    {
        switch (row.Kind)
        {
            case SourceFolderKind.Driver:
                _template.Driver.Folder = absoluteFolder;
                break;
            case SourceFolderKind.Output:
                _template.Output.Folder = absoluteFolder;
                break;
            default:
                var layer = _template.Layers.FirstOrDefault(l => l.Id == row.RoleId);
                if (layer?.Source != null)
                {
                    layer.Source.Folder = absoluteFolder;
                }
                break;
        }
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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Validation;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class SourceViewModel : ObservableObject
{
    private readonly JobPlanner _planner;
    private readonly ValidationEngine _validator;

    [ObservableProperty]
    private string _rootFolder = "";

    [ObservableProperty]
    private ObservableCollection<ValidationIssue> _validationMessages = new();

    [ObservableProperty]
    private string _status = "Sẵn sàng. Nhập đường dẫn folder driver vào ô trên.";

    public SourceViewModel(JobPlanner planner, ValidationEngine validator)
    {
        _planner = planner;
        _validator = validator;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (string.IsNullOrEmpty(RootFolder))
        {
            Status = "Chưa chọn thư mục. Nhập đường dẫn vào ô trên (ví dụ: D:\\videos\\drivers)";
            return;
        }

        if (!Directory.Exists(RootFolder))
        {
            Status = $"Thư mục không tồn tại: {RootFolder}";
            return;
        }

        Status = "Đang quét...";
        ValidationMessages.Clear();

        try
        {
            // TODO: Load template from somewhere (for now use dummy)
            var dummyTemplate = new VideoAutoTool.Core.Templates.Template
            {
                Canvas = new VideoAutoTool.Core.Templates.CanvasSettings { Width = 1280, Height = 720, Fps = 25 },
                Layers = new List<VideoAutoTool.Core.Templates.LayerDefinition>(),
                Output = new VideoAutoTool.Core.Templates.OutputSettings()
            };

            var report = await _validator.RunAsync(dummyTemplate, RootFolder, null, System.Threading.CancellationToken.None);
            
            foreach (var issue in report.Issues)
            {
                ValidationMessages.Add(issue);
            }

            var plan = await _planner.PlanAsync(dummyTemplate, RootFolder, System.Threading.CancellationToken.None);

            Status = $"Tìm thấy {plan.Jobs.Count} job, {ValidationMessages.Count} thông báo ({report.ErrorCount} lỗi, {report.WarningCount} cảnh báo)";
        }
        catch (Exception ex)
        {
            Status = $"Lỗi: {ex.Message}";
        }
    }
}

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class DesignViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "Sẵn sàng thiết kế";

    [ObservableProperty]
    private ObservableCollection<LayerItemViewModel> _layers = new();

    // Two-way bound to the canvas control. The property panel binds directly to
    // this item's observable X/Y/LayerScale/LayerWidth, so canvas and panel stay
    // in sync in both directions without duplicated proxy properties.
    [ObservableProperty]
    private LayerItemViewModel? _selectedLayerItem;

    [ObservableProperty]
    private string _templatePath = "template_draft.json";

    public DesignViewModel()
    {
        // Sample layers for testing - full set
        Layers.Add(new LayerItemViewModel(new LayerDefinition
        {
            Type = LayerType.BackgroundChain,
            Name = "Nền chính",
            Transform = new TransformSettings { X = 0, Y = 0, Scale = 1.0 }
        }));
        Layers.Add(new LayerItemViewModel(new LayerDefinition
        {
            Type = LayerType.LoopVideo,
            Name = "Soundwave",
            Transform = new TransformSettings { X = 50, Y = 50, Scale = 1.0 }
        }));
        Layers.Add(new LayerItemViewModel(new LayerDefinition
        {
            Type = LayerType.Image,
            Name = "Avatar",
            Transform = new TransformSettings { X = 900, Y = 500, Scale = 0.8 }
        }));
        Layers.Add(new LayerItemViewModel(new LayerDefinition
        {
            Type = LayerType.Subtitle,
            Name = "Phụ đề chính",
            Transform = new TransformSettings { X = 340, Y = 600, Scale = 1.0 }
        }));
        Layers.Add(new LayerItemViewModel(new LayerDefinition
        {
            Type = LayerType.FixedText,
            Name = "Text cố định",
            Transform = new TransformSettings { X = 50, Y = 650, Scale = 1.0 }
        }));
    }

    [RelayCommand]
    private async Task SaveDraftAsync()
    {
        try
        {
            // Build template from current layers
            var template = BuildTemplate();
            
            // Serialize to JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(template, options);
            
            // Save to file
            await File.WriteAllTextAsync(TemplatePath, json);
            
            Status = $"Đã lưu draft: {Path.GetFileName(TemplatePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Lỗi khi lưu: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadDraftAsync()
    {
        try
        {
            if (!File.Exists(TemplatePath))
            {
                Status = "Không tìm thấy file draft";
                return;
            }

            // Read and deserialize
            var json = await File.ReadAllTextAsync(TemplatePath);
            var template = JsonSerializer.Deserialize<Template>(json);
            
            if (template?.Layers == null)
            {
                Status = "File draft không hợp lệ";
                return;
            }

            // Load layers
            Layers.Clear();
            foreach (var layer in template.Layers)
            {
                Layers.Add(new LayerItemViewModel(layer));
            }

            Status = $"Đã tải draft: {Path.GetFileName(TemplatePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Lỗi khi tải: {ex.Message}";
        }
    }

    private Template BuildTemplate()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 1280, Height = 720, Fps = 25 },
            Layers = new List<LayerDefinition>(),
            Output = new OutputSettings { Quality = 5 }
        };

        foreach (var layerItem in Layers)
        {
            template.Layers.Add(layerItem.Layer);
        }

        return template;
    }
}

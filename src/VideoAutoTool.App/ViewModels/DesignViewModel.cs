using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.App.Services;
using VideoAutoTool.Core.Design;
using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class DesignViewModel : ObservableObject
{
    private readonly IFontCatalog _fonts;
    private readonly IUiDialogs _dialogs;

    /// <summary>Undo/redo history for canvas edits (drag/resize).</summary>
    public DesignHistory History { get; } = new();

    [ObservableProperty]
    private string _status = "Sẵn sàng thiết kế";

    [ObservableProperty]
    private ObservableCollection<LayerItemViewModel> _layers = new();

    [ObservableProperty]
    private LayerItemViewModel? _selectedLayerItem;

    [ObservableProperty]
    private ObservableCollection<StylePresetItemViewModel> _stylePresets = new();

    [ObservableProperty]
    private StylePresetItemViewModel? _selectedStylePreset;

    [ObservableProperty]
    private ObservableCollection<FontOption> _fontOptions = new();

    [ObservableProperty]
    private FontOption? _selectedFontOption;

    [ObservableProperty]
    private string _templatePath = "template_draft.json";

    public bool IsTextStyleVisible => SelectedLayerItem?.IsTextLayer == true;

    public DesignViewModel(IFontCatalog fonts, IUiDialogs dialogs)
    {
        _fonts = fonts;
        _dialogs = dialogs;

        var template = TemplateDefaults.CreateCo139();
        foreach (var layer in template.Layers)
        {
            Layers.Add(new LayerItemViewModel(layer));
        }

        foreach (var preset in template.StylePresets)
        {
            StylePresets.Add(new StylePresetItemViewModel(preset));
        }

        SelectedStylePreset = StylePresets.FirstOrDefault();
        ApplySamplePreviews(resetBoxes: true);
        ReloadFonts();
    }

    partial void OnSelectedLayerItemChanged(LayerItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsTextStyleVisible));
    }

    partial void OnSelectedFontOptionChanged(FontOption? value)
    {
        if (value is null || SelectedStylePreset is null) return;
        SelectedStylePreset.Font = value.FamilyName;
        SelectedStylePreset.FontSource = value.Source;
    }

    private void ReloadFonts()
    {
        FontOptions.Clear();
        foreach (var entry in _fonts.ListAll())
        {
            var sourceLabel = entry.Source switch
            {
                FontSource.Bundled => "kèm app",
                FontSource.Imported => "đã import",
                _ => "Windows"
            };
            FontOptions.Add(new FontOption(entry.FamilyName, entry.Source, $"{entry.FamilyName} ({sourceLabel})"));
        }

        SyncSelectedFontOption();
    }

    private void SyncSelectedFontOption()
    {
        if (SelectedStylePreset is null)
        {
            SelectedFontOption = null;
            return;
        }

        SelectedFontOption = FontOptions.FirstOrDefault(f =>
            f.FamilyName.Equals(SelectedStylePreset.Font, StringComparison.OrdinalIgnoreCase))
            ?? FontOptions.FirstOrDefault();
    }

    partial void OnSelectedStylePresetChanged(StylePresetItemViewModel? value)
    {
        SyncSelectedFontOption();
        ApplyStyleToSubtitlePreview();
    }

    private void ApplySamplePreviews(bool resetBoxes)
    {
        var avatar = SampleMediaLocator.FindAvatarImage();
        var wave = SampleMediaLocator.FindSoundwaveVideo();
        var srt = SampleMediaLocator.FindSampleSrt();
        var sampleLine = "My child, my beloved, before I utter";
        if (srt != null)
        {
            var cues = SrtParser.ParseFile(srt);
            if (cues.Count > 0) sampleLine = cues[0].Text;
        }

        var preset = SelectedStylePreset?.Preset;

        foreach (var item in Layers)
        {
            item.Layer.Transform ??= new TransformSettings();
            switch (item.Layer.Type)
            {
                case LayerType.BackgroundChain:
                    if (resetBoxes) SetBox(item, 0, 0, 1280, 720);
                    break;
                case LayerType.Image:
                    if (resetBoxes) SetBox(item, 0, 0, 1280, 720);
                    item.PreviewImagePath = avatar;
                    break;
                case LayerType.LoopVideo:
                    if (resetBoxes) SetBox(item, 40, 280, 520, 120);
                    item.PreviewVideoPath = wave;
                    break;
                case LayerType.Subtitle:
                    if (resetBoxes)
                    {
                        var box = preset?.Box;
                        SetBox(item,
                            box?.X ?? 175,
                            box?.Y ?? 465,
                            (int)(box?.Width ?? 480),
                            (int)(box?.Height ?? 220));
                    }
                    item.PreviewText = sampleLine;
                    break;
            }
        }

        ApplyStyleToSubtitlePreview();
        Status = BuildSampleStatus(avatar, wave, srt);
    }

    private void ApplyStyleToSubtitlePreview()
    {
        var preset = SelectedStylePreset?.Preset;
        if (preset is null) return;
        foreach (var item in Layers.Where(l => l.IsTextLayer))
        {
            item.PreviewColor = preset.Color;
            item.PreviewFontSize = Math.Clamp(preset.Size * 2 / 3, 18, 48);
            item.Layer.StyleAssignment ??= new StyleAssignment
            {
                Mode = StyleAssignmentMode.Fixed,
                Presets = [preset.Id]
            };
            item.Layer.StyleAssignment.Presets = [preset.Id];
        }
    }

    private static void SetBox(LayerItemViewModel item, double x, double y, int width, int height)
    {
        item.Layer.Transform ??= new TransformSettings();
        item.Layer.Transform.X = x;
        item.Layer.Transform.Y = y;
        item.Layer.Transform.Width = width;
        item.Layer.Transform.Height = height;
        item.NotifyTransformChanged();
    }

    private static string BuildSampleStatus(string? avatar, string? wave, string? srt)
    {
        var bits = new List<string>();
        if (avatar != null) bits.Add("avatar.png");
        if (wave != null) bits.Add(Path.GetFileName(wave));
        if (srt != null) bits.Add("SRT mẫu");
        return bits.Count == 0
            ? "Không tìm thấy file mẫu trong thư mục mẫu/"
            : "Canvas mẫu: " + string.Join(", ", bits);
    }

    [RelayCommand]
    private void Undo()
    {
        History.Undo();
        Status = History.CanUndo ? "Đã hoàn tác (Ctrl+Z)" : "Đã hoàn tác - không còn bước trước";
    }

    [RelayCommand]
    private void Redo()
    {
        History.Redo();
        Status = "Đã làm lại (Ctrl+Shift+Z)";
    }

    [RelayCommand]
    private void ImportFont()
    {
        var path = _dialogs.PickFontFile();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            FontCatalog.ImportFont(path);
            ReloadFonts();
            if (SelectedStylePreset is not null)
            {
                SelectedStylePreset.Font = Path.GetFileNameWithoutExtension(path);
                SelectedStylePreset.FontSource = FontSource.Imported;
                SyncSelectedFontOption();
            }

            Status = $"Đã import font: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Status = $"Không import được font: {ex.Message}";
        }
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

            StylePresets.Clear();
            foreach (var preset in template.StylePresets)
            {
                StylePresets.Add(new StylePresetItemViewModel(preset));
            }

            SelectedStylePreset = StylePresets.FirstOrDefault();
            ApplySamplePreviews(resetBoxes: false);
            Status = $"Đã tải draft: {Path.GetFileName(TemplatePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Lỗi khi tải: {ex.Message}";
        }
    }

    private Template BuildTemplate()
    {
        var template = TemplateDefaults.CreateCo139();
        template.Layers = Layers.Select(l => l.Layer).ToList();
        template.StylePresets = StylePresets.Select(p => p.Preset).ToList();
        return template;
    }
}

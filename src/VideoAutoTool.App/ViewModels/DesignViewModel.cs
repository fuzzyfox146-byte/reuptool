using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
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

    public event EventHandler? DesignsChanged;

    public event EventHandler? ContentChanged;

    public Template ExportTemplate() => BuildTemplate();

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
        History.Changed += (_, _) => RaiseContentChanged();
        Layers.CollectionChanged += OnLayerCollectionChanged;
        StylePresets.CollectionChanged += OnStyleCollectionChanged;
        foreach (var layer in Layers)
        {
            layer.PropertyChanged += OnLayerPropertyChanged;
        }

        foreach (var preset in StylePresets)
        {
            preset.PropertyChanged += OnStylePresetEdited;
        }
    }

    private void RaiseContentChanged() => ContentChanged?.Invoke(this, EventArgs.Empty);

    private void OnLayerCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (LayerItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnLayerPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (LayerItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnLayerPropertyChanged;
            }
        }

        RaiseContentChanged();
    }

    private void OnStyleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (StylePresetItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnStylePresetEdited;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (StylePresetItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnStylePresetEdited;
            }
        }

        RaiseContentChanged();
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseContentChanged();

    private void OnStylePresetEdited(object? sender, PropertyChangedEventArgs e) => RaiseContentChanged();

    partial void OnSelectedLayerItemChanged(LayerItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsTextStyleVisible));
    }

    partial void OnSelectedFontOptionChanged(FontOption? value)
    {
        if (value is null || SelectedStylePreset is null) return;
        SelectedStylePreset.Font = value.FamilyName;
        SelectedStylePreset.FontSource = value.Source;
        RaiseContentChanged();
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

    partial void OnSelectedStylePresetChanged(StylePresetItemViewModel? oldValue, StylePresetItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnStylePresetPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnStylePresetPropertyChanged;
        }

        SyncSelectedFontOption();
        ApplyStyleToSubtitlePreview();
    }

    private void OnStylePresetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ApplyStyleToSubtitlePreview();
        RaiseContentChanged();
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
            item.PreviewFontSize = Math.Clamp(preset.Size, 8, 200);
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
    private void SaveDraft()
    {
        try
        {
            var template = BuildTemplate();
            TemplatePath = DesignLibrary.NextSavePath(template.Name);
            template.Name = Path.GetFileNameWithoutExtension(TemplatePath);
            TemplateStore.Save(TemplatePath, template);
            DesignsChanged?.Invoke(this, EventArgs.Empty);
            Status = $"Đã lưu bản mới: {Path.GetFileName(TemplatePath)}";
            RaiseContentChanged();
        }
        catch (Exception ex)
        {
            Status = $"Lỗi khi lưu: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LoadDraft()
    {
        try
        {
            var picked = _dialogs.PickOpenJson("Mở bản thiết kế", DesignLibrary.DirectoryPath);
            if (string.IsNullOrWhiteSpace(picked)) return;
            LoadFromFile(picked);
        }
        catch (Exception ex)
        {
            Status = $"Lỗi khi tải: {ex.Message}";
        }
    }

    public void LoadFromFile(string path)
    {
        var template = TemplateStore.Load(path);
        ApplyLoadedTemplate(template);
        TemplatePath = path;
        DesignsChanged?.Invoke(this, EventArgs.Empty);
        Status = $"Đã mở: {Path.GetFileName(path)}";
        RaiseContentChanged();
    }

    public void ApplyTemplate(Template template, string? path)
    {
        ApplyLoadedTemplate(template);
        if (!string.IsNullOrWhiteSpace(path))
        {
            TemplatePath = path;
        }
    }

    public void SaveCurrentDesign()
    {
        var template = BuildTemplate();
        if (string.IsNullOrWhiteSpace(TemplatePath) || !Path.IsPathRooted(TemplatePath))
        {
            TemplatePath = DesignLibrary.NextSavePath(template.Name);
        }

        template.Name = Path.GetFileNameWithoutExtension(TemplatePath);
        TemplateStore.Save(TemplatePath, template);
        DesignsChanged?.Invoke(this, EventArgs.Empty);
        Status = $"Đã lưu: {Path.GetFileName(TemplatePath)}";
    }

    private void ApplyLoadedTemplate(Template template)
    {
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
    }

    public Template BuildTemplate()
    {
        var template = TemplateDefaults.CreateCo139();
        template.Layers = Layers.Select(l => l.Layer).ToList();
        template.StylePresets = StylePresets.Select(p => p.Preset).ToList();
        return template;
    }
}

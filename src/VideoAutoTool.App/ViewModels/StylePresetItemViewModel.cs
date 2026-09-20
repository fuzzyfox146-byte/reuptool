using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class StylePresetItemViewModel : ObservableObject
{
    public StylePreset Preset { get; }

    public StylePresetItemViewModel(StylePreset preset)
    {
        Preset = preset;
    }

    public string Id => Preset.Id;

    public string DisplayName => string.IsNullOrWhiteSpace(Preset.Id) ? "Style" : Preset.Id;

    public string Font
    {
        get => Preset.Font;
        set
        {
            if (Preset.Font == value) return;
            Preset.Font = value;
            OnPropertyChanged();
        }
    }

    public FontSource FontSource
    {
        get => Preset.FontSource;
        set
        {
            if (Preset.FontSource == value) return;
            Preset.FontSource = value;
            OnPropertyChanged();
        }
    }

    public int Size
    {
        get => Preset.Size;
        set
        {
            var clamped = Math.Clamp(value, 8, 200);
            if (Preset.Size == clamped) return;
            Preset.Size = clamped;
            OnPropertyChanged();
        }
    }

    public bool Bold
    {
        get => Preset.Bold;
        set
        {
            if (Preset.Bold == value) return;
            Preset.Bold = value;
            OnPropertyChanged();
        }
    }

    public string Color
    {
        get => Preset.Color;
        set
        {
            if (Preset.Color == value) return;
            Preset.Color = value;
            OnPropertyChanged();
        }
    }

    public string OutlineColor
    {
        get => Preset.OutlineColor;
        set
        {
            if (Preset.OutlineColor == value) return;
            Preset.OutlineColor = value;
            OnPropertyChanged();
        }
    }

    public double Outline
    {
        get => Preset.Outline;
        set
        {
            var clamped = Math.Max(0, value);
            if (Math.Abs(Preset.Outline - clamped) < 0.001) return;
            Preset.Outline = clamped;
            OnPropertyChanged();
        }
    }

    public double Shadow
    {
        get => Preset.Shadow;
        set
        {
            var clamped = Math.Max(0, value);
            if (Math.Abs(Preset.Shadow - clamped) < 0.001) return;
            Preset.Shadow = clamped;
            OnPropertyChanged();
        }
    }
}

public sealed record FontOption(string FamilyName, FontSource Source, string DisplayLabel);

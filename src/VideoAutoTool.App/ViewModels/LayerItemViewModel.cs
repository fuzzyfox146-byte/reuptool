using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.ViewModels;

/// <summary>
/// ViewModel wrapper for LayerDefinition with UI-specific properties.
/// Exposes X/Y/Scale/Width as observable proxies over <see cref="LayerDefinition.Transform"/>
/// so the canvas and the property panel stay in sync in both directions.
/// </summary>
public sealed partial class LayerItemViewModel : ObservableObject
{
    public LayerDefinition Layer { get; }

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _isFrozen;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Optional still image shown on the design canvas (avatar / static).</summary>
    public string? PreviewImagePath { get; set; }

    /// <summary>Optional looping video shown on the design canvas (soundwave).</summary>
    public string? PreviewVideoPath { get; set; }

    [ObservableProperty]
    private string? _previewText;

    [ObservableProperty]
    private string _previewColor = "#F5A623";

    [ObservableProperty]
    private int _previewFontSize = 28;

    /// <summary>X position on the canvas. Ignored when the layer is locked.</summary>
    public double X
    {
        get => Layer.Transform?.X ?? 0;
        set
        {
            Layer.Transform ??= new TransformSettings();
            if (IsLocked) return;
            if (Math.Abs(Layer.Transform.X - value) < 0.001) return;
            Layer.Transform.X = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Y position on the canvas. Ignored when the layer is locked.</summary>
    public double Y
    {
        get => Layer.Transform?.Y ?? 0;
        set
        {
            Layer.Transform ??= new TransformSettings();
            if (IsLocked) return;
            if (Math.Abs(Layer.Transform.Y - value) < 0.001) return;
            Layer.Transform.Y = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Uniform scale factor. Ignored when the layer is locked.</summary>
    public double LayerScale
    {
        get => Layer.Transform?.Scale ?? 1.0;
        set
        {
            Layer.Transform ??= new TransformSettings();
            if (IsLocked) return;
            var clamped = Math.Max(0.1, value);
            if (Math.Abs(Layer.Transform.Scale - clamped) < 0.0001) return;
            Layer.Transform.Scale = clamped;
            OnPropertyChanged();
        }
    }

    /// <summary>Explicit box width in px (independent of height). Ignored when locked.</summary>
    public double LayerWidth
    {
        get => Layer.Transform?.Width ?? 0;
        set
        {
            Layer.Transform ??= new TransformSettings();
            if (IsLocked) return;
            var w = (int)Math.Max(1, value);
            if ((Layer.Transform.Width ?? 0) == w) return;
            Layer.Transform.Width = w;
            OnPropertyChanged();
        }
    }

    /// <summary>Explicit box height in px (independent of width). Ignored when locked.</summary>
    public double LayerHeight
    {
        get => Layer.Transform?.Height ?? 0;
        set
        {
            Layer.Transform ??= new TransformSettings();
            if (IsLocked) return;
            var h = (int)Math.Max(1, value);
            if ((Layer.Transform.Height ?? 0) == h) return;
            Layer.Transform.Height = h;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Pushes the current transform values to the bound UI (used after a
    /// canvas drag/resize so the property panel reflects the new position).
    /// </summary>
    public void NotifyTransformChanged()
    {
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(LayerScale));
        OnPropertyChanged(nameof(LayerWidth));
        OnPropertyChanged(nameof(LayerHeight));
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Layer.Name))
                return Layer.Name;

            return Layer.Type switch
            {
                LayerType.BackgroundChain => "Nền",
                LayerType.Image => "Ảnh/Avatar",
                LayerType.Subtitle => "Phụ đề",
                LayerType.LoopVideo => "Soundwave",
                LayerType.FixedText => "Text cố định",
                LayerType.StaticImage => "Ảnh tĩnh",
                _ => Layer.Type.ToString()
            };
        }
    }

    public bool IsTextLayer => Layer.Type is LayerType.Subtitle or LayerType.FixedText;

    public LayerItemViewModel(LayerDefinition layer)
    {
        Layer = layer;
    }
}

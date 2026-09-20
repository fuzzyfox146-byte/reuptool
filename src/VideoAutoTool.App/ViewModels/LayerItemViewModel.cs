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

    /// <summary>X position on the canvas. Ignored when the layer is locked.</summary>
    public double X
    {
        get => Layer.Transform?.X ?? 0;
        set
        {
            if (Layer.Transform == null || IsLocked) return;
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
            if (Layer.Transform == null || IsLocked) return;
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
            if (Layer.Transform == null || IsLocked) return;
            var clamped = Math.Max(0.1, value);
            if (Math.Abs(Layer.Transform.Scale - clamped) < 0.0001) return;
            Layer.Transform.Scale = clamped;
            OnPropertyChanged();
        }
    }

    /// <summary>Optional explicit width (px). 0 means "not set".</summary>
    public double LayerWidth
    {
        get => Layer.Transform?.Width ?? 0;
        set
        {
            if (Layer.Transform == null || IsLocked) return;
            var w = (int)Math.Max(0, value);
            if ((Layer.Transform.Width ?? 0) == w) return;
            Layer.Transform.Width = w == 0 ? null : w;
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

    public LayerItemViewModel(LayerDefinition layer)
    {
        Layer = layer;
    }
}

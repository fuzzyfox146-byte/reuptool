using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Design;

/// <summary>
/// Command that changes a layer's position.
/// </summary>
public sealed class MoveLayerCommand : IDesignCommand
{
    private readonly LayerDefinition _layer;
    private readonly int _oldX;
    private readonly int _oldY;
    private int _newX;
    private int _newY;

    public MoveLayerCommand(LayerDefinition layer, int oldX, int oldY, int newX, int newY)
    {
        _layer = layer;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }

    public string Description => $"Move {_layer.Type}";

    public void Execute()
    {
        if (_layer.Transform == null)
        {
            _layer.Transform = new TransformSettings();
        }
        _layer.Transform.X = _newX;
        _layer.Transform.Y = _newY;
    }

    public void Undo()
    {
        if (_layer.Transform != null)
        {
            _layer.Transform.X = _oldX;
            _layer.Transform.Y = _oldY;
        }
    }

    public bool CanMergeWith(IDesignCommand other)
    {
        return other is MoveLayerCommand moveCmd && moveCmd._layer == _layer;
    }

    public void MergeWith(IDesignCommand other)
    {
        if (other is MoveLayerCommand moveCmd)
        {
            _newX = moveCmd._newX;
            _newY = moveCmd._newY;
        }
    }
}

/// <summary>
/// Command that changes a layer's scale.
/// </summary>
public sealed class ScaleLayerCommand : IDesignCommand
{
    private readonly LayerDefinition _layer;
    private readonly double _oldScale;
    private double _newScale;

    public ScaleLayerCommand(LayerDefinition layer, double oldScale, double newScale)
    {
        _layer = layer;
        _oldScale = oldScale;
        _newScale = newScale;
    }

    public string Description => $"Scale {_layer.Type}";

    public void Execute()
    {
        if (_layer.Transform == null)
        {
            _layer.Transform = new TransformSettings();
        }
        _layer.Transform.Scale = _newScale;
    }

    public void Undo()
    {
        if (_layer.Transform != null)
        {
            _layer.Transform.Scale = _oldScale;
        }
    }

    public bool CanMergeWith(IDesignCommand other)
    {
        return other is ScaleLayerCommand scaleCmd && scaleCmd._layer == _layer;
    }

    public void MergeWith(IDesignCommand other)
    {
        if (other is ScaleLayerCommand scaleCmd)
        {
            _newScale = scaleCmd._newScale;
        }
    }
}

/// <summary>
/// Command that changes a layer's full box (position + explicit width/height).
/// Used by the design canvas so a single drag or resize gesture is one undo step.
/// </summary>
public sealed class TransformLayerCommand : IDesignCommand
{
    private readonly LayerDefinition _layer;
    private readonly double _oldX;
    private readonly double _oldY;
    private readonly int _oldWidth;
    private readonly int _oldHeight;
    private readonly double _newX;
    private readonly double _newY;
    private readonly int _newWidth;
    private readonly int _newHeight;

    public TransformLayerCommand(
        LayerDefinition layer,
        double oldX, double oldY, int oldWidth, int oldHeight,
        double newX, double newY, int newWidth, int newHeight)
    {
        _layer = layer;
        _oldX = oldX;
        _oldY = oldY;
        _oldWidth = oldWidth;
        _oldHeight = oldHeight;
        _newX = newX;
        _newY = newY;
        _newWidth = newWidth;
        _newHeight = newHeight;
    }

    public string Description => $"Transform {_layer.Type}";

    public void Execute()
    {
        _layer.Transform ??= new TransformSettings();
        _layer.Transform.X = _newX;
        _layer.Transform.Y = _newY;
        _layer.Transform.Width = _newWidth;
        _layer.Transform.Height = _newHeight;
    }

    public void Undo()
    {
        if (_layer.Transform == null) return;
        _layer.Transform.X = _oldX;
        _layer.Transform.Y = _oldY;
        _layer.Transform.Width = _oldWidth;
        _layer.Transform.Height = _oldHeight;
    }

    // Each gesture is recorded as one command, so no merging.
    public bool CanMergeWith(IDesignCommand other) => false;

    public void MergeWith(IDesignCommand other) { }
}

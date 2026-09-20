using VideoAutoTool.Core.Design;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Design;

public sealed class DesignHistoryTests
{
    [Fact]
    public void Execute_AddsCommandToHistory()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image };
        var command = new MoveLayerCommand(layer, 0, 0, 10, 20);

        history.Execute(command);

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(10, layer.Transform?.X);
        Assert.Equal(20, layer.Transform?.Y);
    }

    [Fact]
    public void Undo_ReversesLastCommand()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image, Transform = new TransformSettings { X = 0, Y = 0 } };
        var command = new MoveLayerCommand(layer, 0, 0, 10, 20);

        history.Execute(command);
        history.Undo();

        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal(0, layer.Transform.X);
        Assert.Equal(0, layer.Transform.Y);
    }

    [Fact]
    public void Redo_ReappliesCommand()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image, Transform = new TransformSettings { X = 0, Y = 0 } };
        var command = new MoveLayerCommand(layer, 0, 0, 10, 20);

        history.Execute(command);
        history.Undo();
        history.Redo();

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(10, layer.Transform.X);
        Assert.Equal(20, layer.Transform.Y);
    }

    [Fact]
    public void Execute_AfterUndo_ClearsRedoStack()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image };
        var command1 = new MoveLayerCommand(layer, 0, 0, 10, 20);
        var command2 = new MoveLayerCommand(layer, 10, 20, 30, 40);

        history.Execute(command1);
        history.Undo();
        history.Execute(command2);

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Execute_MergesConsecutiveSimilarCommands()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image };
        
        // Simulate drag: multiple small moves
        history.Execute(new MoveLayerCommand(layer, 0, 0, 5, 5));
        history.Execute(new MoveLayerCommand(layer, 5, 5, 10, 10));
        history.Execute(new MoveLayerCommand(layer, 10, 10, 15, 15));

        // Should merge into single command
        history.Undo();

        Assert.False(history.CanUndo);
        Assert.Equal(0, layer.Transform?.X);
        Assert.Equal(0, layer.Transform?.Y);
    }

    [Fact]
    public void Clear_RemovesAllHistory()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image };
        var command = new MoveLayerCommand(layer, 0, 0, 10, 20);

        history.Execute(command);
        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void GetUndoDescription_ReturnsCommandDescription()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image };
        var command = new MoveLayerCommand(layer, 0, 0, 10, 20);

        history.Execute(command);

        Assert.Contains("Move", history.GetUndoDescription());
    }

    [Fact]
    public void HistorySize_RespectedsMaxLimit()
    {
        var history = new DesignHistory(maxHistorySize: 3);
        var layer1 = new LayerDefinition { Type = LayerType.Image };
        var layer2 = new LayerDefinition { Type = LayerType.Image };
        var layer3 = new LayerDefinition { Type = LayerType.Image };

        // Create commands on different layers to avoid merging
        history.Execute(new ScaleLayerCommand(layer1, 1.0, 1.1));
        history.Execute(new ScaleLayerCommand(layer2, 1.0, 1.2));
        history.Execute(new ScaleLayerCommand(layer3, 1.0, 1.3));
        history.Execute(new ScaleLayerCommand(layer1, 1.1, 1.4));
        history.Execute(new ScaleLayerCommand(layer2, 1.2, 1.5));

        // Should only keep last 3 commands
        int undoCount = 0;
        while (history.CanUndo)
        {
            history.Undo();
            undoCount++;
        }

        Assert.Equal(3, undoCount);
    }

    [Fact]
    public void Changed_EventRaisedOnHistoryModification()
    {
        var history = new DesignHistory();
        var layer = new LayerDefinition { Type = LayerType.Image };
        var command = new MoveLayerCommand(layer, 0, 0, 10, 20);
        
        int eventCount = 0;
        history.Changed += (s, e) => eventCount++;

        history.Execute(command);
        history.Undo();
        history.Redo();
        history.Clear();

        Assert.Equal(4, eventCount);
    }
}

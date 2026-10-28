using System.Text.Json;

namespace VideoAutoTool.Core.FrameBatch;

public sealed class FrameTemplateHistory
{
    private readonly int _maxDepth;
    private readonly List<string> _undoStack = [];
    private readonly List<string> _redoStack = [];
    private string? _currentJson;

    public FrameTemplateHistory(int maxDepth = 60)
    {
        _maxDepth = Math.Max(5, maxDepth);
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Reset(FrameTemplatePreset preset)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentJson = Serialize(preset);
    }

    public bool RecordChange(FrameTemplatePreset preset)
    {
        var nextJson = Serialize(preset);
        if (_currentJson is null)
        {
            _currentJson = nextJson;
            return false;
        }

        if (string.Equals(_currentJson, nextJson, StringComparison.Ordinal))
        {
            return false;
        }

        _undoStack.Add(_currentJson);
        if (_undoStack.Count > _maxDepth)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
        _currentJson = nextJson;
        return true;
    }

    public FrameTemplatePreset? Undo()
    {
        if (_undoStack.Count == 0)
        {
            return null;
        }

        if (_currentJson is not null)
        {
            _redoStack.Add(_currentJson);
        }

        var previous = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _currentJson = previous;
        return Deserialize(previous);
    }

    public FrameTemplatePreset? Redo()
    {
        if (_redoStack.Count == 0)
        {
            return null;
        }

        if (_currentJson is not null)
        {
            _undoStack.Add(_currentJson);
        }

        var next = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _currentJson = next;
        return Deserialize(next);
    }

    private static string Serialize(FrameTemplatePreset preset) =>
        JsonSerializer.Serialize(preset);

    private static FrameTemplatePreset? Deserialize(string json) =>
        JsonSerializer.Deserialize<FrameTemplatePreset>(json);
}

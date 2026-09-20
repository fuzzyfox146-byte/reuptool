namespace VideoAutoTool.Core.Design;

/// <summary>
/// Manages undo/redo history for template design changes.
/// </summary>
public sealed class DesignHistory
{
    private readonly Stack<IDesignCommand> _undoStack = new();
    private readonly Stack<IDesignCommand> _redoStack = new();
    private readonly int _maxHistorySize;

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Creates a new DesignHistory with the specified maximum size.
    /// </summary>
    public DesignHistory(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Executes a command and adds it to the history.
    /// </summary>
    public void Execute(IDesignCommand command)
    {
        command.Execute();

        // Try to merge with the last command if possible
        if (_undoStack.Count > 0)
        {
            var lastCommand = _undoStack.Peek();
            if (lastCommand.CanMergeWith(command))
            {
                // Merge by executing the new command's values on the existing command
                lastCommand.MergeWith(command);
                RaiseChanged();
                return;
            }
        }

        // Add new command to stack
        _undoStack.Push(command);
        _redoStack.Clear();

        // Limit history size
        if (_undoStack.Count > _maxHistorySize)
        {
            var list = _undoStack.ToList();
            _undoStack.Clear();
            foreach (var cmd in list.Take(_maxHistorySize).Reverse())
            {
                _undoStack.Push(cmd);
            }
        }

        RaiseChanged();
    }

    /// <summary>
    /// Undoes the last command.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        RaiseChanged();
    }

    /// <summary>
    /// Redoes the last undone command.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        RaiseChanged();
    }

    /// <summary>
    /// Clears all history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        RaiseChanged();
    }

    /// <summary>
    /// Gets the description of the command that would be undone.
    /// </summary>
    public string? GetUndoDescription()
    {
        return CanUndo ? _undoStack.Peek().Description : null;
    }

    /// <summary>
    /// Gets the description of the command that would be redone.
    /// </summary>
    public string? GetRedoDescription()
    {
        return CanRedo ? _redoStack.Peek().Description : null;
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

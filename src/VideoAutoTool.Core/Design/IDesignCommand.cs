namespace VideoAutoTool.Core.Design;

/// <summary>
/// Represents a reversible command that modifies the template.
/// </summary>
public interface IDesignCommand
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverses the command.
    /// </summary>
    void Undo();

    /// <summary>
    /// Gets the description of this command for debugging.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Checks if this command can be merged with another consecutive command.
    /// For example, multiple move commands during drag can be merged into one.
    /// </summary>
    bool CanMergeWith(IDesignCommand other);

    /// <summary>
    /// Merges this command with another command of the same type.
    /// </summary>
    void MergeWith(IDesignCommand other);
}

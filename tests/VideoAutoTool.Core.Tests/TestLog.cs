using VideoAutoTool.Core.Logging;

namespace VideoAutoTool.Core.Tests;

/// <summary>
/// Simple in-memory logger for testing.
/// </summary>
public sealed class TestLog : ILog
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages;

    public void Info(string message)
    {
        _messages.Add($"[INFO] {message}");
    }

    public void Warn(string message)
    {
        _messages.Add($"[WARN] {message}");
    }

    public void Error(string message)
    {
        _messages.Add($"[ERROR] {message}");
    }

    public void Clear()
    {
        _messages.Clear();
    }
}

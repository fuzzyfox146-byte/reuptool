namespace VideoAutoTool.Core.Logging;

public sealed class ConsoleLog : ILog
{
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");

    public void Warn(string message) => Console.WriteLine($"[WARN] {message}");

    public void Error(string message) => Console.Error.WriteLine($"[ERROR] {message}");
}

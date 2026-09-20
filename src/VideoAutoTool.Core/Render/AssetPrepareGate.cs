using System.Collections.Concurrent;

namespace VideoAutoTool.Core.Render;

/// <summary>
/// Per-key gate so two jobs can prepare different backgrounds at once,
/// while the same cache file is only written by one job.
/// </summary>
public static class AssetPrepareGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static async Task RunAsync(IEnumerable<string> keys, Func<Task> work, CancellationToken cancellationToken)
    {
        var ordered = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => Path.GetFullPath(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var gates = ordered.Select(k => Gates.GetOrAdd(k, static _ => new SemaphoreSlim(1, 1))).ToList();
        foreach (var gate in gates)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            for (var i = gates.Count - 1; i >= 0; i--)
            {
                gates[i].Release();
            }
        }
    }
}

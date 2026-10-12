namespace VideoAutoTool.Core.FrameBatch;

public interface IFrameQueueSlot
{
    string IntakeId { get; }

    string Status { get; }

    bool Promoted { get; set; }
}

/// <summary>
/// Same wave rule as the Opt queue: each source folder contributes one batch,
/// then the next folder, and the wave repeats only after that batch is in progress.
/// </summary>
public static class FrameIntake
{
    public const int DefaultBatchSize = 3;

    public static int ClampBatch(int batchSize) => Math.Clamp(batchSize, 1, 99);

    public static bool HasPromotedPending(IEnumerable<IFrameQueueSlot> jobs) =>
        jobs.Any(job => job.Promoted && job.Status == FrameDesign.Wait);

    public static void PromoteInitial(IEnumerable<IFrameQueueSlot> fresh, int batchSize)
    {
        foreach (var job in fresh.Where(job => job.Status == FrameDesign.Wait).Take(ClampBatch(batchSize)))
        {
            job.Promoted = true;
        }
    }

    public static void PromoteNextWave(IReadOnlyList<IFrameQueueSlot> jobs, IReadOnlyList<string> intakeOrder, int batchSize)
    {
        if (batchSize <= 0 || HasPromotedPending(jobs))
        {
            return;
        }

        var size = ClampBatch(batchSize);
        foreach (var intakeId in intakeOrder)
        {
            var taken = 0;
            foreach (var job in jobs)
            {
                if (taken >= size)
                {
                    break;
                }

                if (job.IntakeId == intakeId && !job.Promoted && job.Status == FrameDesign.Wait)
                {
                    job.Promoted = true;
                    taken++;
                }
            }
        }
    }
}

public sealed class FrameQueueSlot : IFrameQueueSlot
{
    public required string IntakeId { get; init; }

    public string Status { get; set; } = FrameDesign.Wait;

    public bool Promoted { get; set; }
}

public static class FramePressure
{
    public static bool IsRecoverable(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("No space left on device", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("not enough space", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("because it is being used", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

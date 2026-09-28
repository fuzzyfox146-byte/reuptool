namespace VideoAutoTool.Core.Queue;

public sealed record QueueBatchFinished(int Completed, int Failed, int MovedCount, string? RenderedFolder);

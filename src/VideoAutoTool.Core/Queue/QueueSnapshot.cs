using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Persisted render queue: job rows plus the plans/templates needed to render again.
/// </summary>
public sealed class QueueSnapshot
{
    public int Version { get; set; } = 1;

    public List<RenderJobItem> Jobs { get; set; } = [];

    public Dictionary<string, RenderJobPlan> PlansByJobId { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, Template> TemplateByIntake { get; set; } = new(StringComparer.Ordinal);

    public List<string> IntakeOrder { get; set; } = [];

    public int IntakeSequence { get; set; }

    public int BatchSize { get; set; }

    public int LastBatchSize { get; set; }
}

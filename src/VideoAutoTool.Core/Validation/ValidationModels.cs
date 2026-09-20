namespace VideoAutoTool.Core.Validation;

public enum ValidationLevel
{
    Error,
    Warning,
    Info
}

public sealed record ValidationIssue(
    string Code,
    ValidationLevel Level,
    string Scope,
    string Message,
    string FixHint);

public sealed class ValidationReport
{
    public List<ValidationIssue> Issues { get; } = [];

    public int ErrorCount => Issues.Count(i => i.Level == ValidationLevel.Error);

    public int WarningCount => Issues.Count(i => i.Level == ValidationLevel.Warning);

    public int InfoCount => Issues.Count(i => i.Level == ValidationLevel.Info);

    public bool CanRender(int videoIndex) =>
        !Issues.Any(i => i.Level == ValidationLevel.Error &&
                         i.Scope.Equals($"video:{videoIndex}", StringComparison.Ordinal));

    public IReadOnlyList<ValidationIssue> ForLayer(string layerId) =>
        Issues.Where(i => i.Scope.StartsWith($"layer:{layerId}", StringComparison.Ordinal)).ToList();

    public IReadOnlyList<ValidationIssue> ForVideo(int videoIndex) =>
        Issues.Where(i => i.Scope.Equals($"video:{videoIndex}", StringComparison.Ordinal)).ToList();

    public void SortDeterministic() =>
        Issues.Sort((a, b) =>
        {
            var code = string.Compare(a.Code, b.Code, StringComparison.Ordinal);
            if (code != 0)
            {
                return code;
            }

            return string.Compare(a.Scope, b.Scope, StringComparison.Ordinal);
        });
}

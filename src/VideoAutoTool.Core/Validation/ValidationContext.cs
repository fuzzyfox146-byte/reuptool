using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Validation;

public sealed class ValidationContext
{
    public required Template Template { get; init; }

    public required string Root { get; init; }

    public required IReadOnlyList<ScannedFile> Drivers { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<ScannedFile>> LayerFiles { get; init; } =
        new Dictionary<string, IReadOnlyList<ScannedFile>>();

    public IReadOnlyDictionary<int, ScannedFile> SubByNumber { get; init; } =
        new Dictionary<int, ScannedFile>();

    public IReadOnlyList<ScannedFile> Subs { get; init; } = [];
}

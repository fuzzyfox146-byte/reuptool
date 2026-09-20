using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Validation;

public sealed class ValidationEngine
{
    private readonly IMediaProbe _probe;
    private readonly IFontCatalog _fontCatalog;
    private readonly Func<FfmpegPaths> _ffmpegLocator;

    public ValidationEngine(IMediaProbe probe, IFontCatalog fontCatalog, Func<FfmpegPaths>? ffmpegLocator = null)
    {
        _probe = probe;
        _fontCatalog = fontCatalog;
        _ffmpegLocator = ffmpegLocator ?? (() => FfmpegLocator.Locate());
    }

    public async Task<ValidationReport> RunAsync(
        Template template,
        string root,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var report = new ValidationReport();
        try
        {
            _ = _ffmpegLocator();
        }
        catch (FfmpegNotFoundException)
        {
            report.Issues.Add(ValidationRules.E001());
            return report;
        }

        var ctx = BuildContext(template, root);

        foreach (var layer in template.Layers.Where(l => l.Source is not null))
        {
            var folder = FileScanner.ResolveFolder(root, layer.Source!.Folder);
            if (!Directory.Exists(folder))
            {
                report.Issues.Add(ValidationRules.E010(layer.Id, layer.Source.Folder));
            }
            else if (layer.Type is LayerType.Image or LayerType.LoopVideo or LayerType.BackgroundChain &&
                     ctx.LayerFiles.GetValueOrDefault(layer.Id)?.Count == 0)
            {
                report.Issues.Add(ValidationRules.W012(layer.Id));
            }
        }

        if (ctx.Drivers.Count == 0)
        {
            report.Issues.Add(ValidationRules.E011());
        }

        ValidationRules.ValidateDuplicateSubs(ctx, report.Issues);
        ValidationRules.ValidateFonts(template, _fontCatalog, report.Issues);

        progress?.Report("background");
        await ValidationRules.ValidateBackgroundsAsync(ctx, _probe, report.Issues, cancellationToken).ConfigureAwait(false);
        progress?.Report("avatar");
        await ValidationRules.ValidateAvatarsAsync(ctx, _probe, report.Issues, cancellationToken).ConfigureAwait(false);
        progress?.Report("wave");
        await ValidationRules.ValidateWavesAsync(ctx, _probe, report.Issues, cancellationToken).ConfigureAwait(false);
        progress?.Report("drivers");
        await ValidationRules.ValidateDriverSubsAsync(ctx, _probe, report.Issues, cancellationToken).ConfigureAwait(false);
        progress?.Report("output");
        ValidationRules.ValidateOutput(template, root, ctx.Drivers, _probe, report.Issues);

        report.SortDeterministic();
        return report;
    }

    private static ValidationContext BuildContext(Template template, string root)
    {
        var drivers = FileScanner.ScanFolder(root, template.Driver.Folder, template.Driver.Extensions, template.Driver.NumberPattern);
        var layerFiles = new Dictionary<string, IReadOnlyList<ScannedFile>>();
        foreach (var layer in template.Layers.Where(l => l.Source is not null))
        {
            layerFiles[layer.Id] = FileScanner.ScanFolder(root, layer.Source!.Folder, layer.Source.Extensions, template.Driver.NumberPattern);
        }

        var subLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.Subtitle);
        var subs = subLayer?.Source is null
            ? []
            : FileScanner.ScanFolder(root, subLayer.Source.Folder, subLayer.Source.Extensions, template.Driver.NumberPattern);
        var subByNumber = subs
            .Where(s => s.Number.HasValue)
            .GroupBy(s => s.Number!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.FileName, new NaturalSortComparer()).First());

        return new ValidationContext
        {
            Template = template,
            Root = root,
            Drivers = drivers,
            LayerFiles = layerFiles,
            SubByNumber = subByNumber,
            Subs = subs
        };
    }
}

using System.Text.RegularExpressions;
using VideoAutoTool.Core.Scanning;

namespace VideoAutoTool.Core.Subtitles;

/// <summary>
/// Pairs a source video with an SRT by title. The same leading number is not enough.
/// An SRT may add a trailing language code such as ".en" or ".de" before the extension.
/// </summary>
public static class SubtitleNameMatcher
{
    public readonly record struct Result(ScannedFile? Match, ScannedFile? SameNumberMismatch, int MatchCount);

    public static bool IsMatch(string driverFileName, string srtFileName) =>
        string.Equals(DriverKey(driverFileName), SubtitleKey(srtFileName), StringComparison.OrdinalIgnoreCase);

    public static string DriverKey(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).Trim();

    public static string SubtitleKey(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        var dot = stem.LastIndexOf('.');
        if (dot > 0 && IsLanguageSuffix(stem[(dot + 1)..]))
        {
            stem = stem[..dot].TrimEnd();
        }

        return stem;
    }

    private static bool IsLanguageSuffix(string suffix) =>
        Regex.IsMatch(suffix, "^[A-Za-z]{2,3}(-[A-Za-z]{2,8})?$");

    public static Result Resolve(ScannedFile driver, IReadOnlyList<ScannedFile> subs)
    {
        var matches = subs
            .Where(s => IsMatch(driver.FileName, s.FileName))
            .OrderBy(s => s.FileName, new NaturalSortComparer())
            .ToList();

        ScannedFile? chosen = null;
        if (matches.Count > 0)
        {
            chosen = driver.Number is int number
                ? matches.FirstOrDefault(s => s.Number == number) ?? matches[0]
                : matches[0];
        }

        ScannedFile? wrongNumber = null;
        if (chosen is null && driver.Number is int num)
        {
            wrongNumber = subs
                .Where(s => s.Number == num)
                .OrderBy(s => s.FileName, new NaturalSortComparer())
                .FirstOrDefault();
        }

        return new Result(chosen, wrongNumber, matches.Count);
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace YtFolderUploader;

public static partial class FileNames
{
    public const int TitleLimit = 100;

    public static bool TryGetIndex(string fileName, out int number, out string raw)
    {
        var name = Path.GetFileName(fileName);
        var match = LeadingIndexRegex().Match(name);
        if (!match.Success)
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            if (OnlyIndexRegex().IsMatch(stem))
            {
                raw = stem;
                number = int.Parse(stem, CultureInfo.InvariantCulture);
                return true;
            }

            number = 0;
            raw = "";
            return false;
        }

        raw = match.Groups["n"].Value;
        number = int.Parse(raw, CultureInfo.InvariantCulture);
        return true;
    }

    public static bool IsBeforeStart(string? index, int startIndex)
    {
        if (startIndex <= 0 || string.IsNullOrWhiteSpace(index))
            return false;
        if (!int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return false;
        return number < startIndex;
    }

    public static (string Title, bool Trimmed) MakeTitle(string fileName, bool stripKhung)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Normalize(NormalizationForm.FormC);
        stem = LeadingIndexRegex().Replace(stem, "", 1);
        if (OnlyIndexRegex().IsMatch(stem))
            stem = "";
        if (stripKhung)
            stem = KhungSuffixRegex().Replace(stem, "");

        var trimmedToLimit = false;
        stem = Sanitize(stem);
        if (CountRunes(stem) > TitleLimit)
        {
            stem = TakeRunes(stem, TitleLimit).TrimEnd();
            trimmedToLimit = true;
        }

        return (stem, trimmedToLimit);
    }

    public static string SanitizeTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var direct = Sanitize(raw.Normalize(NormalizationForm.FormC));
        if (CountRunes(direct) > TitleLimit)
            direct = TakeRunes(direct, TitleLimit).TrimEnd();
        return direct.Trim();
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.00} GB";
    }

    static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is '<' or '>')
                continue;
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
                continue;
            builder.Append(rune.ToString());
        }

        return builder.ToString().Trim();
    }

    static int CountRunes(string value) => value.EnumerateRunes().Count();

    static string TakeRunes(string value, int count)
    {
        var builder = new StringBuilder();
        var taken = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (taken == count)
                break;
            builder.Append(rune.ToString());
            taken++;
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^(?<n>\d{1,4})(?:\s+|[_-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingIndexRegex();

    [GeneratedRegex(@"^\d{1,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex OnlyIndexRegex();

    [GeneratedRegex(@"_khung_\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KhungSuffixRegex();
}

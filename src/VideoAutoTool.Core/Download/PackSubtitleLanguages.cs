using System.Globalization;
using System.Text;

namespace VideoAutoTool.Core.Download;

public static class PackSubtitleLanguages
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["anh"] = "en",
        ["english"] = "en",
        ["tienganh"] = "en",
        ["nhat"] = "ja",
        ["japanese"] = "ja",
        ["tiengnhat"] = "ja",
        ["bo"] = "pt",
        ["bodaonha"] = "pt",
        ["portuguese"] = "pt",
        ["tiengbo"] = "pt",
        ["han"] = "ko",
        ["korean"] = "ko",
        ["tienghan"] = "ko",
        ["trung"] = "zh",
        ["chinese"] = "zh",
        ["tiengtrung"] = "zh",
        ["taybannha"] = "es",
        ["spanish"] = "es",
        ["phap"] = "fr",
        ["french"] = "fr",
        ["duc"] = "de",
        ["german"] = "de",
        ["viet"] = "vi",
        ["vietnamese"] = "vi",
        ["tiengviet"] = "vi",
        ["thai"] = "th",
        ["tiengthai"] = "th",
        ["arab"] = "ar",
        ["arabic"] = "ar",
        ["tatca"] = "all",
        ["moingonngu"] = "all",
        ["all"] = "all"
    };

    public static string ForYtDlp(string? text)
    {
        var codes = Codes(text);
        if (codes.Count == 0)
        {
            return "ja";
        }

        if (codes.Count == 1 && codes[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return "all";
        }

        return string.Join(",", codes);
    }

    public static string OrigFallback(IReadOnlyList<string> missing)
    {
        if (missing.Count == 0 || missing.Any(code => code.Equals("all", StringComparison.OrdinalIgnoreCase)))
        {
            return "";
        }

        return string.Join(",", missing.Select(code => code + "-orig"));
    }

    public static IReadOnlyList<string> DuplicateNames(IEnumerable<string> fileNames, int number, string? languages)
    {
        var files = fileNames
            .Where(name => DownloadResume.IsSubtitleFile(name) && LeadingNumber(name) == number)
            .ToList();
        var delete = new List<string>();
        foreach (var group in files.GroupBy(name => GroupKey(Suffix(name), Codes(languages))))
        {
            var keep = group
                .OrderBy(name => Rank(name, group.Key))
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .First();
            delete.AddRange(group.Where(name => !name.Equals(keep, StringComparison.OrdinalIgnoreCase)));
        }

        return delete;
    }

    public static IReadOnlyList<string> Codes(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return list;
        }

        foreach (var part in text.Split([',', ';', '/', '+', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var piece in part.Split(" và ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var code = ToCode(piece);
                if (code is null || list.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(code);
            }
        }

        if (list.Any(code => code.Equals("all", StringComparison.OrdinalIgnoreCase)))
        {
            return ["all"];
        }

        return list;
    }

    public static IReadOnlyList<string> Missing(string folder, int number, string? text)
    {
        var codes = Codes(text);
        if (codes.Count == 0)
        {
            codes = ["ja"];
        }

        if (codes.Count == 1 && codes[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return DownloadResume.HasCompleted(folder, number, DownloadResume.IsSubtitleFile) ? [] : ["all"];
        }

        return codes.Where(code => !HasFile(folder, number, code)).ToList();
    }

    public static bool HasFile(string folder, int number, string language)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        foreach (var path in Directory.EnumerateFiles(folder))
        {
            var name = Path.GetFileName(path);
            if (!DownloadResume.IsSubtitleFile(name) || LeadingNumber(name) != number)
            {
                continue;
            }

            if (Matches(name, language))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ToCode(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Equals("jp", StringComparison.OrdinalIgnoreCase))
        {
            return "ja";
        }

        if (trimmed.Equals("kr", StringComparison.OrdinalIgnoreCase))
        {
            return "ko";
        }

        if (Names.TryGetValue(Fold(trimmed), out var code))
        {
            return code;
        }

        return IsCode(trimmed) ? trimmed.ToLowerInvariant() : null;
    }

    private static bool IsCode(string token) =>
        System.Text.RegularExpressions.Regex.IsMatch(token, "^[A-Za-z]{2,3}(-[A-Za-z]{2,8})?$");

    private static string GroupKey(string? suffix, IReadOnlyList<string> codes)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return "";
        }

        foreach (var code in codes.OrderByDescending(code => code.Length))
        {
            if (suffix.Equals(code, StringComparison.OrdinalIgnoreCase)
                || suffix.StartsWith(code + "-", StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        var dash = suffix.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? suffix[..dash] : suffix;
    }

    private static int Rank(string fileName, string code)
    {
        var suffix = Suffix(fileName) ?? "";
        var kind = suffix.Equals(code, StringComparison.OrdinalIgnoreCase)
            ? 0
            : suffix.EndsWith("-orig", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        var ext = Path.GetExtension(fileName).Equals(".srt", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        return kind * 10 + ext;
    }

    private static string? Suffix(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var dot = stem.LastIndexOf('.');
        return dot < 0 || dot == stem.Length - 1 ? null : stem[(dot + 1)..];
    }

    private static bool Matches(string fileName, string language)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var dot = stem.LastIndexOf('.');
        if (dot < 0 || dot == stem.Length - 1)
        {
            return false;
        }

        var suffix = stem[(dot + 1)..];
        if (suffix.Equals(language, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var requested = language.Split('-')[0];
        var actual = suffix.Split('-')[0];
        return actual.Equals(requested, StringComparison.OrdinalIgnoreCase)
            && !language.Contains('-', StringComparison.Ordinal);
    }

    private static int? LeadingNumber(string name)
    {
        var end = 0;
        while (end < name.Length && char.IsDigit(name[end]))
        {
            end++;
        }

        if (end == 0 || !int.TryParse(name[..end], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        return number;
    }

    private static string Fold(string value)
    {
        var form = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(form.Length);
        foreach (var c in form)
        {
            if (c is 'đ')
            {
                builder.Append('d');
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && c is not (' ' or '-' or '_'))
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

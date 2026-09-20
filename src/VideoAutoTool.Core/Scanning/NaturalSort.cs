using System.Globalization;
using System.Text.RegularExpressions;

namespace VideoAutoTool.Core.Scanning;

public static class NaturalSort
{
    private static readonly Regex TokenRegex = new(@"\d+|\D+", RegexOptions.Compiled);

    public static int Compare(string? left, string? right) =>
        Compare(ReadOnlySpan<char>.Empty, left ?? string.Empty, right ?? string.Empty);

    public static IEnumerable<T> OrderByNatural<T>(IEnumerable<T> items, Func<T, string> selector) =>
        items.OrderBy(selector, Comparer<string>.Create(Compare));

    private static int Compare(ReadOnlySpan<char> _, string x, string y)
    {
        var xTokens = Tokenize(x);
        var yTokens = Tokenize(y);
        var count = Math.Min(xTokens.Count, yTokens.Count);
        for (var i = 0; i < count; i++)
        {
            var a = xTokens[i];
            var b = yTokens[i];
            var aIsNum = int.TryParse(a, out var aNum);
            var bIsNum = int.TryParse(b, out var bNum);
            if (aIsNum && bIsNum)
            {
                var cmp = aNum.CompareTo(bNum);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            else
            {
                var cmp = string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
        }

        return xTokens.Count.CompareTo(yTokens.Count);
    }

    private static List<string> Tokenize(string value) =>
        TokenRegex.Matches(value).Select(m => m.Value).ToList();
}

public sealed class NaturalSortComparer : IComparer<string>
{
    public int Compare(string? x, string? y) => NaturalSort.Compare(x, y);
}

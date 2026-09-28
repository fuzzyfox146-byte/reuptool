using System.Text;

namespace VideoAutoTool.Core.Download;

public static class DownloadTitleKey
{
    public static string Compact(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name).Trim();
        var start = 0;
        while (start < stem.Length && char.IsDigit(stem[start]))
        {
            start++;
        }

        stem = stem[start..].Trim();
        var dot = stem.LastIndexOf('.');
        if (dot > 0 && stem[(dot + 1)..].Length is >= 2 and <= 3)
        {
            var suffix = stem[(dot + 1)..];
            if (suffix.All(char.IsLetter))
            {
                stem = stem[..dot].TrimEnd();
            }
        }

        var builder = new StringBuilder(stem.Length);
        foreach (var ch in stem)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    public static bool Aligns(string leftName, string rightName)
    {
        var left = Compact(leftName);
        var right = Compact(rightName);
        if (left.Length == 0 || right.Length == 0)
        {
            return false;
        }

        if (left.Length < 8 || right.Length < 8)
        {
            return left == right;
        }

        var length = Math.Min(Math.Min(left.Length, right.Length), 60);
        return left.StartsWith(right[..length], StringComparison.Ordinal)
            || right.StartsWith(left[..length], StringComparison.Ordinal);
    }
}

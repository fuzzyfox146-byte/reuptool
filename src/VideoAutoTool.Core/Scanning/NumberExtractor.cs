using System.Text.RegularExpressions;

namespace VideoAutoTool.Core.Scanning;

public static class NumberExtractor
{
    public static int? Extract(string fileName, string numberPattern)
    {
        var name = Path.GetFileName(fileName);
        var match = Regex.Match(name, numberPattern);
        if (!match.Success || match.Groups.Count < 2)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var number) ? number : null;
    }
}

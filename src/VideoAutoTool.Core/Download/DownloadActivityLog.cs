using System.Text.RegularExpressions;

namespace VideoAutoTool.Core.Download;

public sealed record DownloadNotice(
    string? ReplaceKey,
    string Text,
    string? Folder = null,
    string? Kind = null,
    string? FileNumber = null);

/// <summary>
/// Turns yt-dlp console lines into one row per file: name plus the latest speed.
/// </summary>
public sealed partial class DownloadActivityLog
{
    private readonly IProgress<DownloadNotice>? _log;
    private readonly string _kind;
    private readonly string _scope;
    private readonly string _folderName;
    private string? _file;
    private string? _speed;
    private string? _key;
    private bool _openTemp;
    private int _seq;

    public DownloadActivityLog(IProgress<DownloadNotice>? log, string kind, string parentFolder)
    {
        _log = log;
        _kind = kind;
        _scope = parentFolder;
        _folderName = Path.GetFileName(parentFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public void OnLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        line = Ansi().Replace(line, "").Trim();
        if (TryDestination(line, out var path))
        {
            var file = DisplayName(path);
            var temp = IsTemp(file);
            if (_key is null || !_openTemp)
            {
                _seq++;
                _key = _scope + "|" + _kind + "|" + _seq;
                _speed = null;
            }

            _openTemp = temp;
            _file = file;
            Publish();
            return;
        }

        if (_file is null || _key is null)
        {
            return;
        }

        var speed = Speed().Match(line);
        if (!speed.Success)
        {
            return;
        }

        _speed = speed.Groups["speed"].Value;
        Publish();
    }

    private void Publish()
    {
        if (_file is null || _key is null)
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(_folderName)
            ? $"{_kind}: {_file}"
            : $"{_folderName}  {_kind}: {_file}";
        if (!string.IsNullOrWhiteSpace(_speed))
        {
            text += "  " + _speed;
        }

        _log?.Report(new DownloadNotice(_key, text, _scope, _kind, LeadingNumber(_file)));
    }

    private static string? LeadingNumber(string file)
    {
        var end = 0;
        while (end < file.Length && char.IsDigit(file[end]))
        {
            end++;
        }

        return end == 0 ? null : file[..end];
    }

    private static bool TryDestination(string line, out string path)
    {
        var match = Destination().Match(line);
        if (!match.Success)
        {
            path = "";
            return false;
        }

        path = match.Groups["path"].Value.Trim().Trim('"');
        return path.Length > 0 && !IsImage(path);
    }

    private static string DisplayName(string path)
    {
        var file = Path.GetFileName(path);
        if (file.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
        {
            file = file[..^".part".Length];
        }

        if (file.EndsWith(".json3", StringComparison.OrdinalIgnoreCase))
        {
            file = Path.ChangeExtension(file, ".srt");
        }

        return file;
    }

    private static bool IsTemp(string file) =>
        file.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
        || Fragment().IsMatch(file);

    private static bool IsImage(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex Ansi();

    [GeneratedRegex(@"\.(?:f\d+)\.", RegexOptions.IgnoreCase)]
    private static partial Regex Fragment();

    [GeneratedRegex(@"\[download\]\s+(?:\d+(?:\.\d+)?%).*?\bat\s+(?<speed>\d+(?:\.\d+)?[KMG]?i?B/s)", RegexOptions.IgnoreCase)]
    private static partial Regex Speed();

    [GeneratedRegex(@"^(?:\[download\] Destination:\s+(?<path>.+)|\[download\]\s+(?<path>.+?)\s+has already been downloaded|\[Merger\] Merging formats into ""(?<path>.+)""|\[info\] Writing video (?:subtitles|automatic captions) to:\s+(?<path>.+))$", RegexOptions.IgnoreCase)]
    private static partial Regex Destination();
}

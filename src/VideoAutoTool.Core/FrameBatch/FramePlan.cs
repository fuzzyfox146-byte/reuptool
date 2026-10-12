using System.Security.Cryptography;
using System.Text;

namespace VideoAutoTool.Core.FrameBatch;

public sealed record FramePair(string Video, string Frame, string Background, string OutputName);

public static class FramePlan
{
    public static IReadOnlyList<string> ListMedia(string folder, IReadOnlyCollection<string> extensions)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException("Không thấy thư mục: " + folder);
        }

        return Directory.EnumerateFiles(folder)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string SourceLabel(string sourceFolder)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder));
        var name = Path.GetFileName(full);
        if (name.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Path.GetFileName(Path.GetDirectoryName(full) ?? "");
            if (!string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }
        }

        return name;
    }

    public static string? SiblingOutputFolder(string sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!Path.GetFileName(full).Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(full);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        return Path.Combine(parent, "xuat");
    }

    public static int StableSeed(IEnumerable<string> frames, IEnumerable<string> backgrounds)
    {
        var raw = string.Join('\n', frames.Concat(backgrounds).Select(Path.GetFullPath));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return BitConverter.ToInt32(hash, 0) & int.MaxValue;
    }

    public static IReadOnlyList<FramePair> Assign(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> frames,
        IReadOnlyList<string> backgrounds,
        bool random,
        int seed,
        IReadOnlyList<FramePair>? previous)
    {
        var frameOrder = frames.ToList();
        var backgroundOrder = backgrounds.ToList();
        if (random)
        {
            Shuffle(frameOrder, seed);
            Shuffle(backgroundOrder, seed + 1);
        }

        var saved = new Dictionary<string, FramePair>(StringComparer.OrdinalIgnoreCase);
        if (previous is not null)
        {
            foreach (var item in previous)
            {
                saved[Path.GetFullPath(item.Video)] = item;
            }
        }

        var frameSet = frames.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var backgroundSet = backgrounds.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = new FramePair?[videos.Count];
        var keptCount = 0;
        for (var i = 0; i < videos.Count; i++)
        {
            if (saved.TryGetValue(Path.GetFullPath(videos[i]), out var old)
                && frameSet.Contains(Path.GetFullPath(old.Frame))
                && backgroundSet.Contains(Path.GetFullPath(old.Background)))
            {
                kept[i] = old;
                keptCount++;
            }
        }

        var cursor = keptCount;
        var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var planned = new List<FramePair>(videos.Count);
        for (var i = 0; i < videos.Count; i++)
        {
            var existing = kept[i];
            if (existing is null)
            {
                var frame = frameOrder[cursor % frameOrder.Count];
                var background = backgroundOrder[cursor % backgroundOrder.Count];
                cursor++;
                existing = new FramePair(Path.GetFullPath(videos[i]), Path.GetFullPath(frame), Path.GetFullPath(background), "");
            }

            var name = OutputName(existing.Video, existing.Frame);
            usedNames.TryGetValue(name, out var count);
            count++;
            usedNames[name] = count;
            if (count > 1)
            {
                name = Path.GetFileNameWithoutExtension(name) + "_" + count + ".mp4";
            }

            planned.Add(existing with { OutputName = name });
        }

        return planned;
    }

    public static string OutputName(string video, string frame)
    {
        static string Clean(string stem)
        {
            var cleaned = new string(stem.Select(ch => "<>:\"/\\|?*".Contains(ch) ? '_' : ch).ToArray()).Trim();
            return cleaned.Length == 0 ? "clip" : cleaned;
        }

        return Clean(Path.GetFileNameWithoutExtension(video)) + "_" + Clean(Path.GetFileNameWithoutExtension(frame)) + ".mp4";
    }

    public static string Fingerprint(string path, string parameters)
    {
        var info = new FileInfo(path);
        var raw = info.FullName + "|" + info.LastWriteTimeUtc.Ticks + "|" + info.Length + "|" + parameters;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public static string CacheFile(string folder, string source, string suffix)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(source))))[..8];
        return Path.Combine(folder, Path.GetFileNameWithoutExtension(source) + "_" + digest + suffix);
    }

    private static void Shuffle<T>(IList<T> items, int seed)
    {
        var random = new Random(seed);
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swap = random.Next(i + 1);
            (items[i], items[swap]) = (items[swap], items[i]);
        }
    }
}

namespace VideoAutoTool.Core.Download;

public static class VttToSrt
{
    public static int ConvertFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var converted = 0;
        foreach (var vttPath in Directory.EnumerateFiles(folder, "*.vtt").ToList())
        {
            var srtPath = Path.ChangeExtension(vttPath, ".srt");
            if (!File.Exists(srtPath))
            {
                File.WriteAllText(srtPath, Convert(File.ReadAllText(vttPath)));
                converted++;
            }

            File.Delete(vttPath);
        }

        return converted;
    }

    public static string Convert(string vtt)
    {
        var lines = vtt.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var cues = new List<string>();
        var buffer = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                Flush(buffer, cues);
                continue;
            }

            if (line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("STYLE", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Kind:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Language:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            buffer.Add(line);
        }

        Flush(buffer, cues);
        return string.Join("\r\n\r\n", cues) + (cues.Count > 0 ? "\r\n" : "");
    }

    private static void Flush(List<string> buffer, List<string> cues)
    {
        var time = buffer.FindIndex(line => line.Contains("-->", StringComparison.Ordinal));
        if (time >= 0)
        {
            var text = string.Join("\r\n", buffer.Skip(time + 1).Where(line => line.Length > 0));
            if (text.Length > 0)
            {
                cues.Add((cues.Count + 1) + "\r\n" + buffer[time].Replace('.', ',') + "\r\n" + text);
            }
        }

        buffer.Clear();
    }
}

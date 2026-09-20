namespace VideoAutoTool.Core.Cache;

public static class ConcatListBuilder
{
    public static string Create(IReadOnlyList<string> filePaths, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        var listPath = Path.Combine(tempDir, $"concat_{Guid.NewGuid():N}.txt");
        
        using var writer = new StreamWriter(listPath, append: false, System.Text.Encoding.UTF8);
        foreach (var path in filePaths)
        {
            var absolutePath = Path.GetFullPath(path);
            var escapedPath = absolutePath.Replace("'", @"'\''");
            writer.WriteLine($"file '{escapedPath}'");
        }
        
        return listPath;
    }
}

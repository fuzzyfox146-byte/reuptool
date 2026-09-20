namespace VideoAutoTool.Core.Cache;

public sealed class CacheStore
{
    private readonly string _cacheDir;

    public CacheStore()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoAutoTool",
            "cache");
        Directory.CreateDirectory(_cacheDir);
    }

    public string GetPath(string key, string extension)
    {
        if (!extension.StartsWith('.'))
        {
            extension = '.' + extension;
        }
        var path = Path.Combine(_cacheDir, key + extension);
        
        // Touch file for LRU tracking
        if (File.Exists(path))
        {
            try
            {
                File.SetLastAccessTime(path, DateTime.UtcNow);
            }
            catch
            {
                // Ignore errors when touching file
            }
        }
        
        return path;
    }

    public bool Exists(string key, string extension)
    {
        var path = GetPath(key, extension);
        return File.Exists(path);
    }

    public (int Count, long TotalBytes) GetInfo()
    {
        if (!Directory.Exists(_cacheDir))
        {
            return (0, 0);
        }

        var files = Directory.GetFiles(_cacheDir);
        var count = files.Length;
        var totalBytes = files.Sum(f =>
        {
            try
            {
                return new FileInfo(f).Length;
            }
            catch
            {
                return 0L;
            }
        });

        return (count, totalBytes);
    }

    public void Clear()
    {
        if (!Directory.Exists(_cacheDir))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(_cacheDir))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore errors when deleting
            }
        }
    }

    public void CleanupLRU(long maxBytes)
    {
        if (!Directory.Exists(_cacheDir))
        {
            return;
        }

        var files = Directory.GetFiles(_cacheDir)
            .Select(f => new FileInfo(f))
            .Where(fi => fi.Exists)
            .OrderBy(fi =>
            {
                try
                {
                    return fi.LastAccessTimeUtc;
                }
                catch
                {
                    return DateTime.MinValue;
                }
            })
            .ToList();

        var totalSize = files.Sum(fi => fi.Length);

        foreach (var file in files)
        {
            if (totalSize <= maxBytes)
            {
                break;
            }

            try
            {
                var size = file.Length;
                file.Delete();
                totalSize -= size;
            }
            catch
            {
                // Ignore errors when deleting
            }
        }
    }

    public string GetTempPath(string key, string extension)
    {
        if (!extension.StartsWith('.'))
        {
            extension = '.' + extension;
        }
        // ffmpeg chooses muxer from the LAST suffix; ".mp4.tmp" is treated as .tmp and fails.
        return Path.Combine(_cacheDir, key + ".tmp" + extension);
    }

    public void CommitTemp(string tempPath, string finalPath)
    {
        File.Move(tempPath, finalPath, overwrite: true);
    }
}

using VideoAutoTool.Core.Cache;
using Xunit;

namespace VideoAutoTool.Core.Tests.Cache;

public class CacheStoreTests
{
    [Fact]
    public void GetInfo_EmptyCache_ReturnsZero()
    {
        var store = new CacheStore();
        store.Clear();

        var (count, totalBytes) = store.GetInfo();

        Assert.Equal(0, count);
        Assert.Equal(0, totalBytes);
    }

    [Fact]
    public void Exists_NonExistentKey_ReturnsFalse()
    {
        var store = new CacheStore();
        
        Assert.False(store.Exists("nonexistent", ".mp4"));
    }

    [Fact]
    public void GetPath_TouchesExistingFile()
    {
        var store = new CacheStore();
        var key = Guid.NewGuid().ToString();
        var path = store.GetPath(key, ".mp4");

        File.WriteAllText(path, "test");
        var timeBefore = File.GetLastAccessTimeUtc(path);

        Thread.Sleep(100);
        var path2 = store.GetPath(key, ".mp4");
        var timeAfter = File.GetLastAccessTimeUtc(path2);

        Assert.True(timeAfter > timeBefore);

        File.Delete(path);
    }

    [Fact]
    public void CommitTemp_MovesFile()
    {
        var store = new CacheStore();
        var key = Guid.NewGuid().ToString();
        var tempPath = store.GetTempPath(key, ".mp4");
        var finalPath = store.GetPath(key, ".mp4");
        Assert.EndsWith(".tmp.mp4", tempPath);
        Assert.False(tempPath.EndsWith(".mp4.tmp", StringComparison.OrdinalIgnoreCase));

        File.WriteAllText(tempPath, "test content");
        store.CommitTemp(tempPath, finalPath);

        Assert.False(File.Exists(tempPath));
        Assert.True(File.Exists(finalPath));
        Assert.Equal("test content", File.ReadAllText(finalPath));

        File.Delete(finalPath);
    }

    [Fact]
    public void CleanupLRU_RemovesOldestFiles()
    {
        var store = new CacheStore();
        store.Clear();

        var key1 = Guid.NewGuid().ToString();
        var key2 = Guid.NewGuid().ToString();
        var key3 = Guid.NewGuid().ToString();

        var path1 = store.GetPath(key1, ".mp4");
        var path2 = store.GetPath(key2, ".mp4");
        var path3 = store.GetPath(key3, ".mp4");

        File.WriteAllText(path1, new string('a', 1000));
        Thread.Sleep(100);
        File.WriteAllText(path2, new string('b', 1000));
        Thread.Sleep(100);
        File.WriteAllText(path3, new string('c', 1000));

        // Touch file 2 to make it most recent
        Thread.Sleep(100);
        File.SetLastAccessTimeUtc(path2, DateTime.UtcNow);

        // Cleanup to 2000 bytes max
        store.CleanupLRU(2000);

        // File 1 should be deleted (oldest), file 2 and 3 should remain
        Assert.False(File.Exists(path1));
        Assert.True(File.Exists(path2));
        Assert.True(File.Exists(path3));

        File.Delete(path2);
        File.Delete(path3);
    }
}

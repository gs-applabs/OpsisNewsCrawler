using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class ResourceRetentionTests
{
    [Fact]
    public void GeneratedCachePruningLeavesUserFilesAndNewestCacheIntact()
    {
        var root = Path.Combine(Path.GetTempPath(), "crawler-resource-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var old = DateTime.UtcNow.AddDays(-1);
            for (int i = 0; i < DataCache.MaxCacheFiles + 50; i++)
            {
                var path = Path.Combine(root, $"news-{i}.json");
                File.WriteAllText(path, "{}");
                File.SetLastWriteTimeUtc(path, old.AddSeconds(i));
            }
            var user = Path.Combine(root, "config.json");
            var history = Path.Combine(root, "quotes-cache-ohlc.json.backup");
            File.WriteAllText(user, "keep");
            File.WriteAllText(history, "keep");
            var newest = Path.Combine(root, $"news-{DataCache.MaxCacheFiles + 49}.json");
            DataCache.TrimGeneratedCache(root, newest);
            Assert.Equal(DataCache.MaxCacheFiles, Directory.GetFiles(root, "news-*.json").Length);
            Assert.True(File.Exists(newest));
            Assert.Equal("keep", File.ReadAllText(user));
            Assert.Equal("keep", File.ReadAllText(history));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void GeneratedCachePruningBoundsTotalBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "crawler-resource-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (int i = 0; i < 6; i++)
            {
                var path = Path.Combine(root, $"news-{i}.json");
                using var stream = File.Create(path);
                stream.SetLength(DataCache.MaxEntryBytes);
            }
            DataCache.TrimGeneratedCache(root);
            Assert.True(Directory.GetFiles(root).Sum(p => new FileInfo(p).Length) <= DataCache.MaxCacheBytes);
        }
        finally { Directory.Delete(root, true); }
    }
}

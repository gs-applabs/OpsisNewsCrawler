using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P0SsdWritesTests
{
    [Fact]
    public void UnchangedFilesAreNotRewritten()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "prices.json");
        try
        {
            Assert.True(SafeFile.WriteAllTextIfChanged(file, "{\"today\":12}"));
            var first = File.GetLastWriteTimeUtc(file);
            Assert.False(SafeFile.WriteAllTextIfChanged(file, "{\"today\":12}"));
            Assert.Equal(first, File.GetLastWriteTimeUtc(file));
            Assert.True(SafeFile.WriteAllTextIfChanged(file, "{\"today\":13}"));
            Assert.Equal("{\"today\":13}", File.ReadAllText(file));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void IdenticalPricesDoNotSerializeOrRewriteHistoryAndChangesAreCoalesced()
    {
        var dir = Path.Combine(Path.GetTempPath(), "crawler-quotes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "prices.json");
        try
        {
            var store = new QuoteStore(path);
            var now = DateTimeOffset.UtcNow;
            store.Record("X", "20261127", 10m);
            store.SaveIfDue(now);
            var first = File.ReadAllText(path);
            var time = File.GetLastWriteTimeUtc(path);
            for (int i = 0; i < 10000; i++)
            {
                store.Record("X", "20261127", 10m);
                store.SaveIfDue(now.AddMinutes(i));
            }
            Assert.Equal(time, File.GetLastWriteTimeUtc(path));
            Assert.Equal(first, File.ReadAllText(path));
            store.Record("X", "20261127", 11m);
            store.SaveIfDue(now.AddSeconds(15));
            Assert.Equal(first, File.ReadAllText(path));
            store.Record("X", "20261127", 12m);
            store.SaveIfDue(now.AddSeconds(30));
            Assert.Equal(first, File.ReadAllText(path));
            store.SaveIfDue(now.AddMinutes(1));
            Assert.Equal(12m, new QuoteStore(path).History("X").Single());
            store.Record("X", "20261127", 13m);
            store.Save();
            Assert.Equal(13m, new QuoteStore(path).History("X").Single());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ConcurrentAtomicWritesDoNotLeaveTemporaryFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "crawler-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            Parallel.For(0, 100, i => SafeFile.WriteAllText(path, "{\"value\":" + i + "}"));
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Assert.InRange(doc.RootElement.GetProperty("value").GetInt32(), 0, 99);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { Directory.Delete(dir, true); }
    }
}

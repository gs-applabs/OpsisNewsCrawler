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
}

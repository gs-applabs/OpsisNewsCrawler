using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class BoundedLocalFileTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void EnforcesByteLimitOnFile(int length, bool accepted)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[length]);
            var result = BoundedLocalFile.Read(path, 16);
            if (accepted) Assert.Equal(length, Assert.IsType<byte[]>(result).Length);
            else Assert.Null(result);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DataRootPreservesDefaultAndExplicitProbeFolder()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpsisNewsCrawler"), CrawlerDataPaths.Resolve(null));
        var probe = Path.Combine(Path.GetTempPath(), "isolated-crawler");
        Assert.Equal(probe, CrawlerDataPaths.Resolve(probe));
    }
}

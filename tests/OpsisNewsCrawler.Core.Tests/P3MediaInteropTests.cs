using OpsisNewsCrawler.Core;
using Xunit;
namespace OpsisNewsCrawler.Core.Tests;
public sealed class P3MediaInteropTests
{
    [Fact] public void PlaylistExportsOnlyValidHttpsAndKeepsAttribution()
    {
        var playlist=MediaSuiteBridge.ExportM3u(new[]{
            new MediaSuiteAsset("https://example.org/live.m3u8","Channel", "Source",null),
            new MediaSuiteAsset("http://127.0.0.1/test","Private","Other",null)
        });
        Assert.Contains("https://example.org/live.m3u8",playlist);
        Assert.Contains("Source — Channel",playlist);
        Assert.DoesNotContain("127.0.0.1",playlist);
    }
}

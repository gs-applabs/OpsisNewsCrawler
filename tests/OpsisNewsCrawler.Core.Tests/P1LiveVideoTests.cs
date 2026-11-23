using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1LiveVideoTests
{
    [Fact]
    public void RelevantUserConfiguredChannelWinsAndDuplicatesAreRemoved()
    {
        var urls = new[] {
            new LiveFeed("General", "https://example.org/general"),
            new LiveFeed("Earthquake bulletin", "https://example.org/live") {
                EventKeywords = new() { "EARTHQUAKE" }, Region = "GR", Priority = 2
            },
            new LiveFeed("Duplicate", "https://example.org/live") { Priority = 99 },
            new LiveFeed("Disabled", "https://example.org/no") { Enabled = false }
        };
        var selected = LiveVideoPolicy.Select(urls, "Breaking: earthquake in Greece", "GR");
        Assert.Equal(2, selected.Count);
        Assert.Equal("Earthquake bulletin", selected[0].Name);
    }

    [Theory]
    [InlineData("https://youtube.com/watch?v=abcdefghijk", "https://www.youtube.com/embed/abcdefghijk?autoplay=1&mute=1&rel=0")]
    [InlineData("https://youtu.be/abcdefghijk", "https://www.youtube.com/embed/abcdefghijk?autoplay=1&mute=1&rel=0")]
    public void YoutubeLinksAreMutedEmbeds(string input, string expected) =>
        Assert.Equal(expected, Breaking.ToPlayableUrl(input));

    [Fact]
    public void FakeYoutubeHostsCannotTriggerTrustedEmbedConversion()
    {
        Assert.Equal("https://notyoutube.com/watch?v=abcdefghijk",
            Breaking.ToPlayableUrl("https://notyoutube.com/watch?v=abcdefghijk"));
        Assert.Null(Breaking.ToPlayableUrl("http://127.0.0.1/live"));
    }
}

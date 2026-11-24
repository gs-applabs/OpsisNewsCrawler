using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1LiveVideoStudioTests
{
    [Fact]
    public void PreservePriorityRegionKeywordsDuringUiConfigurationCopy()
    {
        var original = new LiveFeed("News", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")
        {
            Priority = 75,
            Region = "gr",
            EventKeywords = new() { "Athens", "ΑΘΗΝΑ", "athens" },
            Enabled = false
        };
        var cleaned = LiveVideoStudio.Clean(original);
        Assert.Equal(75, cleaned.Priority);
        Assert.Equal("GR", cleaned.Region);
        Assert.Equal(2, cleaned.EventKeywords.Count);
        Assert.False(cleaned.Enabled);
        Assert.Equal(original.Url, cleaned.Url);
    }

    [Fact]
    public void InvalidMetadataIsClampedAndNonPlayableFeedsAreRemovedFromActiveCatalogue()
    {
        var original = new LiveFeed("User TV", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")
        {
            Priority = int.MaxValue, Region = "../../../", EventKeywords = new() { "x", new string('a', 61), "urgent" }
        };
        var clean = LiveVideoStudio.Clean(original);
        Assert.Equal(100, clean.Priority);
        Assert.Empty(clean.Region);
        Assert.Equal(new[] { "urgent" }, clean.EventKeywords);
        Assert.DoesNotContain(LiveVideoStudio.CleanAll(new[] { original, new LiveFeed("Invalid", "file:///etc/passwd"),
            new LiveFeed("Overlong", "https://example.org/" + new string('x', 3000)) }),
            x => x.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || x.Url.Length > 2048);
    }

    [Fact]
    public void RankingStillUsesOnlyExplicitUserConfiguredStreams()
    {
        var candidates = LiveVideoStudio.CleanAll(new[] {
            new LiveFeed("A", "https://www.youtube.com/watch?v=dQw4w9WgXcQ") { Priority = 0 },
            new LiveFeed("B", "https://www.youtube.com/watch?v=9bZkp7q19f0") { Priority = 15, Region = "CY" }
        });
        var ranked = LiveVideoPolicy.Select(candidates, "Breaking", "CY");
        Assert.Equal("B", ranked[0].Name);
    }
}

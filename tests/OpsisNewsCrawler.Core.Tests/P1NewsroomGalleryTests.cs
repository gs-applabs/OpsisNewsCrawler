using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1NewsroomGalleryTests
{
    [Theory]
    [InlineData("news", true)]
    [InlineData("breaking", true)]
    [InlineData("history", true)]
    [InlineData("weather", false)]
    [InlineData("markets", false)]
    public void GalleryIsAvailableOnlyForRelevantExistingWidgets(string kind, bool allowed) =>
        Assert.Equal(allowed, WidgetStudio.CanUseGallery(kind));

    [Fact]
    public void GalleryLayoutImportsAreValidatedAgainstWidgetKind()
    {
        var raw = new Dictionary<string, WidgetStudioProfile>
        {
            ["news"] = new() { Layout = "gallery", MaxItems = 17 },
            ["weather"] = new() { Layout = "gallery", MaxItems = 15 },
            ["history"] = new() { Layout = "gallery", ShowTime = true }
        };
        var clean = WidgetStudio.CleanAll(raw);
        Assert.Equal("gallery", clean["news"].Layout);
        Assert.Equal(17, clean["news"].MaxItems);
        Assert.Equal("headlines", clean["weather"].Layout);
        Assert.False(clean["history"].ShowTime);
    }

    [Fact]
    public void GalleryMetadataIsPlainTextSafeAndBounded()
    {
        var text = WidgetStudio.GalleryCardText(new string('A', 220) + "\nBad",
            new string('B', 220) + "\rline", "Source\nFake", DateTimeOffset.UtcNow,
            new WidgetStudioProfile { Layout = "gallery", ShowSource = true, ShowTime = false },
            true, false, false);
        Assert.DoesNotContain("Source\nFake", text);
        Assert.DoesNotContain("\r", text);
        Assert.StartsWith(new string('A', 109) + "…", text);
        Assert.True(text.Length <= 350);
    }

    [Fact]
    public void BreakingCardsAreClearlyLabeledWithoutTruthScore()
    {
        var text = WidgetStudio.GalleryCardText("Earthquake", "Reported by media", "Agency",
            DateTimeOffset.UtcNow, new WidgetStudioProfile { Layout = "gallery" },
            false, true, false);
        Assert.Contains("BREAKING", text);
        Assert.DoesNotContain("100%", text);
        Assert.Contains("Earthquake", text);
    }

    [Fact]
    public void HistoricEntriesNeverGetCurrentNewsPublicationTimes()
    {
        var now = DateTimeOffset.UtcNow;
        var text = WidgetStudio.GalleryCardText("480 π.Χ.", "Ελληνική ιστορία", "Chronikologio",
            now, new WidgetStudioProfile { Layout = "gallery", ShowTime = true },
            true, false, true);
        Assert.DoesNotContain(now.ToLocalTime().ToString("HH:mm"), text);
    }
}

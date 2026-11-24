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
    public void MarketsUseTheExistingQuoteDataWithoutRealtimeClaims()
    {
        var quote = WidgetStudio.MarketCardText("Χρυσός", "€ 1.234,50 +1.00% · daily",
            new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero), +1,
            new WidgetStudioProfile { Layout = "market-cards", ShowTime = true }, true);
        Assert.Contains("Χρυσός", quote);
        Assert.Contains("↑", quote);
        Assert.Contains("Τελευταία αναφορά", quote);
        Assert.Contains("daily", quote);
        Assert.DoesNotContain("ζωνταν", quote, StringComparison.OrdinalIgnoreCase);
        var changed = WidgetStudio.MarketCardText("EUR/USD", "1.12 -0.2%", DateTimeOffset.MinValue, -1, null, false);
        Assert.Contains("↓", changed);
        Assert.DoesNotContain("Last reported", changed);
    }

    [Fact]
    public void UnknownMarketCardLayoutsCannotLeakToOtherWidgets()
    {
        var input = new Dictionary<string, WidgetStudioProfile>
        {
            ["markets"] = new() { Layout = "market-cards" },
            ["breaking"] = new() { Layout = "market-cards" }
        };
        var cleaned = WidgetStudio.CleanAll(input);
        Assert.Equal("market-cards", cleaned["markets"].Layout);
        Assert.Equal("headlines", cleaned["breaking"].Layout);
    }

    [Fact]
    public void RelatedReportCountIsProvenanceNotVerification()
    {
        var text = WidgetStudio.GalleryCardText("<b>Story</b>",
            "Details &amp; more", "Agency", DateTimeOffset.UtcNow,
            new WidgetStudioProfile { Layout = "gallery", ShowSource = true },
            false, false, false, relatedReports: 4);
        Assert.Contains("Story", text);
        Assert.Contains("Details & more", text);
        Assert.Contains("4 related reports", text);
        Assert.DoesNotContain("<b>", text);
        Assert.DoesNotContain("verified", text, StringComparison.OrdinalIgnoreCase);
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

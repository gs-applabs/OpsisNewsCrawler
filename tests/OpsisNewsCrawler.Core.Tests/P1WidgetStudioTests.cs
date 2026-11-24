using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1WidgetStudioTests
{
    [Fact]
    public void DefaultWidgetProfilesPreserveTheExistingFiveWidgetKeys()
    {
        var settings = new AppConfig();
        Assert.Equal(new[] { "news", "markets", "weather", "breaking", "history" }, WidgetStudio.Kinds);
        Assert.All(WidgetStudio.Kinds, key =>
        {
            var profile = WidgetStudio.Resolve(settings.WidgetProfiles, key);
            Assert.Equal("headlines", profile.Layout);
            Assert.Equal(12, profile.MaxItems);
            Assert.False(profile.ShowSource);
            Assert.False(profile.ShowTime);
        });
    }

    [Fact]
    public void ImportCleansUnknownProfilesAndUnboundedValues()
    {
        var dictionary = new Dictionary<string, WidgetStudioProfile>
        {
            ["BREAKING"] = new() { Layout = "context", MaxItems = int.MaxValue, ShowSource = true },
            ["mystery"] = new() { Layout = "invalid", MaxItems = -5 }
        };
        var cleaned = WidgetStudio.CleanAll(dictionary);
        Assert.Single(cleaned);
        Assert.Equal(30, cleaned["breaking"].MaxItems);
        Assert.Equal("context", cleaned["breaking"].Layout);
        Assert.True(cleaned["breaking"].ShowSource);
        Assert.Equal("headlines", WidgetStudio.Resolve(null, "news").Layout);
    }

    [Fact]
    public void HeadlineModePreservesOriginalBehaviorAndContextModeAddsLabelsOnlyWhenRequested()
    {
        var now = DateTimeOffset.UtcNow;
        var old = WidgetStudio.FormatLine("Story", "Publisher", now, null);
        Assert.Equal("Story", old);
        var context = WidgetStudio.FormatLine("Story", "Publisher", now,
            new WidgetStudioProfile { Layout = "context", ShowSource = true, ShowTime = false });
        Assert.Equal("Publisher  —  Story", context);
        var future = WidgetStudio.FormatLine("Future", "Source", now.AddDays(3),
            new WidgetStudioProfile { Layout = "context", ShowTime = true });
        Assert.Equal("Future", future);
    }

    [Fact]
    public void NewlineAndExtremelyLongTitlesCannotDistortWidgetRow()
    {
        var line = WidgetStudio.FormatLine(new string('X', 200) + "\r\nSPOOF",
            "News", DateTimeOffset.UtcNow, null);
        Assert.DoesNotContain("\n", line);
        Assert.True(line.Length <= 160);
        Assert.EndsWith("…", line);
        var context = WidgetStudio.FormatLine("Title", "News\nFake headline",
            DateTimeOffset.UtcNow, new WidgetStudioProfile { Layout = "context", ShowSource = true });
        Assert.DoesNotContain("\n", context);
        var history = WidgetStudio.FormatLine("490 π.Χ.", "Χρονολόγιο",
            DateTimeOffset.UtcNow, new WidgetStudioProfile { Layout = "context", ShowTime = true }, allowTime: false);
        Assert.DoesNotContain(" — ", history); // historical year is in title; publication time is suppressed
    }
}

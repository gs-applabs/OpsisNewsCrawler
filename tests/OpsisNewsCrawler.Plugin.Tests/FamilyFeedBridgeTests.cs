using OpsisNewsCrawler.Windows;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>Chronikologio and the Crawler as separate programs: the "On this day" lines travel through a small file, no Opsis needed.</summary>
[Collection("PluginFeeds")]
public sealed class FamilyFeedBridgeTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "opsis-feeds-" + Guid.NewGuid().ToString("N"));
    private static readonly DateOnly Today = new(2026, 11, 26);

    public FamilyFeedBridgeTests() { Directory.CreateDirectory(dir); FamilyFeedBridge.Reset(); PluginFeeds.Unregister(FamilyFeedBridge.FeedKey); }
    public void Dispose() { FamilyFeedBridge.Reset(); PluginFeeds.Unregister(FamilyFeedBridge.FeedKey); Directory.Delete(dir, true); }

    private void Write(string json) => File.WriteAllText(Path.Combine(dir, FamilyFeedBridge.FileName), json);
    private static string Json(string date, params string[] lines) =>
        "{\"schema\":1,\"key\":\"san-simera\",\"titleEl\":\"Σαν σήμερα\",\"titleEn\":\"On this day\",\"days\":[{\"date\":\"" + date + "\",\"lines\":[" +
        string.Join(",", lines.Select(l => System.Text.Json.JsonSerializer.Serialize(l))) + "]}]}";

    [Fact]
    public void TodaysLinesBecomeTheFeedAndAnOldFileShowsNothing()
    {
        Write(Json("2026-11-26", "1821 — Η Επανάσταση", "Αγίου Νικολάου"));
        Assert.True(FamilyFeedBridge.Sync(Today, dir));
        var feed = Assert.IsType<PluginFeed>(PluginFeeds.Find("san-simera"));
        Assert.Equal(new[] { "1821 — Η Επανάσταση", "Αγίου Νικολάου" }, feed.GetItems().Select(i => i.Text));
        Assert.False(FamilyFeedBridge.Sync(Today, dir));                         // unchanged: no new registration
        Assert.True(FamilyFeedBridge.Sync(Today.AddDays(8), dir));               // the file has no entry for that day: the feed goes away
        Assert.Null(PluginFeeds.Find("san-simera"));
    }

    [Fact]
    public void APluginsFeedAlwaysWins()
    {
        var plugin = new PluginFeed("san-simera", "Σαν σήμερα", "On this day", () => new[] { new PluginFeedItem("from the plugin") });
        PluginFeeds.Register(plugin);
        Write(Json("2026-11-26", "from the file"));
        Assert.False(FamilyFeedBridge.Sync(Today, dir));
        Assert.Same(plugin, PluginFeeds.Find("san-simera"));
        Assert.Equal("from the plugin", PluginFeeds.Find("san-simera")!.GetItems()[0].Text);
    }

    [Fact]
    public void ABridgedFeedStepsAsideWhenAPluginRegistersLater()
    {
        Write(Json("2026-11-26", "from the file"));
        Assert.True(FamilyFeedBridge.Sync(Today, dir));
        var plugin = new PluginFeed("san-simera", "Σαν σήμερα", "On this day", () => new[] { new PluginFeedItem("from the plugin") });
        PluginFeeds.Register(plugin);
        Assert.False(FamilyFeedBridge.Sync(Today, dir));
        Assert.Same(plugin, PluginFeeds.Find("san-simera"));
    }

    [Fact]
    public void MissingRegistryEntryIsRestoredWithoutChangingTheFile()
    {
        Write(Json("2026-11-26", "from file"));
        Assert.True(FamilyFeedBridge.Sync(Today, dir));
        PluginFeeds.Unregister(FamilyFeedBridge.FeedKey);
        Assert.True(FamilyFeedBridge.Sync(Today, dir));
        Assert.Equal("from file", PluginFeeds.Find(FamilyFeedBridge.FeedKey)!.GetItems()[0].Text);
    }

    [Fact]
    public void TitleOnlyChangeRefreshesFeed()
    {
        var json = Json("2026-11-26", "same lines");
        Write(json);
        Assert.True(FamilyFeedBridge.Sync(Today, dir));
        var old = PluginFeeds.Find(FamilyFeedBridge.FeedKey);
        Write(json.Replace("On this day", "History today — updated"));
        Assert.True(FamilyFeedBridge.Sync(Today, dir));
        Assert.NotSame(old, PluginFeeds.Find(FamilyFeedBridge.FeedKey));
    }

    [Theory]
    [InlineData("{\"schema\":1.5,\"key\":\"san-simera\",\"days\":[]}")]
    [InlineData("not json")]
    [InlineData("{\"schema\":2,\"key\":\"san-simera\",\"days\":[]}")]
    [InlineData("{\"schema\":1,\"key\":\"other\",\"days\":[{\"date\":\"2026-11-26\",\"lines\":[\"x\"]}]}")]
    [InlineData("{\"schema\":1,\"key\":\"san-simera\",\"days\":[{\"date\":\"2026-11-26\",\"lines\":[]}]}")]
    [InlineData("")]
    public void MalformedOrForeignFilesAreIgnored(string json)
    {
        Write(json);
        Assert.False(FamilyFeedBridge.Sync(Today, dir));
        Assert.Null(PluginFeeds.Find("san-simera"));
    }

    [Fact]
    public void TextIsBoundedAndControlCharactersRemoved()
    {
        Write(Json("2026-11-26", "a\r\nb\u0007c", new string('x', 1000)));
        FamilyFeedBridge.Sync(Today, dir);
        var items = PluginFeeds.Find("san-simera")!.GetItems();
        Assert.Equal("a b c", items[0].Text);
        Assert.True(items[1].Text.Length <= 301);
        File.WriteAllBytes(Path.Combine(dir, FamilyFeedBridge.FileName), new byte[300 * 1024]);   // over the size cap
        Assert.True(FamilyFeedBridge.Sync(Today, dir));   // the feed is removed
        Assert.Null(PluginFeeds.Find("san-simera"));
    }
}

[CollectionDefinition("PluginFeeds", DisableParallelization = true)]
public sealed class PluginFeedsCollection { }

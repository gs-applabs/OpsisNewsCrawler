using System.Windows.Forms;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>Smoke test: the new 0.29.0 windows can be built and driven without a desktop session (not a visual check).</summary>
public sealed class Forms029Tests
{
    private static void OnSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null) throw new Xunit.Sdk.XunitException(error.ToString());
    }

    [Fact]
    public void FocusReaderAndSourceComparisonBuildForNormalAndEmptyInput() => OnSta(() =>
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            new NewsItem("First headline of the story", "https://a.example/1", "A", now, new string('x', 3000)),
            new NewsItem("Second headline of the story", "https://b.example/2", "B", now.AddHours(-1))
        };
        using (var reader = new FocusReaderForm(items, 99)) { reader.CreateControl(); Assert.True(reader.Controls.Count >= 4); }
        using (var empty = new FocusReaderForm(Array.Empty<NewsItem>())) empty.CreateControl();
        var cluster = new StoryCluster(items[0].Url, items[0].Title, now, items, 2);
        using var compare = new P3EvidenceForm(cluster);
        compare.CreateControl();
        Assert.True(compare.Controls.Count >= 3);
    });
}

using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>A refresh must not move what the reader is looking at.</summary>
public sealed class ScrollAnchorTests
{
    private static StripEntry[] Strip(params (string Id, int Width)[] items)
    {
        var x = 0;
        return items.Select(i => { var e = new StripEntry(i.Id, x, i.Width); x += i.Width; return e; }).ToArray();
    }

    private static int Loop(StripEntry[] s) => s.Sum(e => e.Width);

    /// <summary>The pixel of entry <paramref name="id"/> relative to the left edge.</summary>
    private static double Where(StripEntry[] s, double offset, string id) => s.First(e => e.Id == id).X - offset;

    [Fact]
    public void NewHeadlinesInFrontDoNotMoveTheVisibleOne()
    {
        var before = Strip(("a", 100), ("b", 120), ("c", 80));
        var after = Strip(("new1", 90), ("new2", 110), ("a", 100), ("b", 120), ("c", 80));
        var offset = 130.0;   // 30 px into "b"
        var next = ScrollAnchor.Relocate(before, Loop(before), offset, after, Loop(after));
        Assert.Equal(Where(before, offset, "b"), Where(after, next, "b"));
        Assert.Equal(330, next);
    }

    [Fact]
    public void ChangingPriceWidthsKeepsTheSameQuoteAtTheEdge()
    {
        var before = Strip(("q1", 100), ("q2", 100), ("q3", 100));
        var after = Strip(("q1", 112), ("q2", 95), ("q3", 130));
        var next = ScrollAnchor.Relocate(before, Loop(before), 150, after, Loop(after));
        Assert.Equal(112 + 50, next);                                        // still 50 px into q2
    }

    [Fact]
    public void ARemovedEntryHandsOverToTheNextSurvivorAtTheSameDistance()
    {
        var before = Strip(("a", 100), ("b", 100), ("c", 100), ("d", 100));
        var after = Strip(("c", 100), ("d", 100), ("e", 100));
        var offset = 130.0;   // inside "b", which is gone; "c" starts 70 px ahead
        var next = ScrollAnchor.Relocate(before, Loop(before), offset, after, Loop(after));
        Assert.Equal(Where(before, offset, "c"), Where(after, next, "c") >= 0 ? Where(after, next, "c") : Where(after, next, "c") + Loop(after));
    }

    [Fact]
    public void SameListGivesTheSamePositionAndEdgeCasesAreSafe()
    {
        var s = Strip(("a", 100), ("b", 100));
        Assert.Equal(150, ScrollAnchor.Relocate(s, 200, 150, s, 200));
        Assert.Equal(150, ScrollAnchor.Relocate(s, 200, 350, s, 200));        // wraps
        Assert.Equal(0, ScrollAnchor.Relocate(s, 200, 150, Array.Empty<StripEntry>(), 0));
        Assert.Equal(40, ScrollAnchor.Relocate(Array.Empty<StripEntry>(), 0, 240, s, 200));
        Assert.Equal(30, ScrollAnchor.Relocate(s, 200, 30, Strip(("x", 50), ("y", 50)), 100));   // nothing in common: same pixel
        Assert.Equal(0, ScrollAnchor.Relocate(s, 200, double.NaN, s, 200));
    }

    [Fact]
    public void RepeatedIdentitiesAreMatchedInOrder()
    {
        var before = Strip(("a", 50), ("x", 50), ("a", 50));
        var after = Strip(("n", 40), ("a", 50), ("x", 50), ("a", 50));
        var next = ScrollAnchor.Relocate(before, Loop(before), 110, after, Loop(after));   // 10 px into the second "a"
        Assert.Equal(40 + 100 + 10, next);
    }

    [Fact]
    public void IdentityIgnoresTheChangingText()
    {
        var q1 = new NewsItem("10,00 +1%", "", "ALPHA", DateTimeOffset.UtcNow, "Alpha Bank", Kind: NewsItemKind.Quote);
        var q2 = q1 with { Title = "10,15 +2%", PublishedAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        Assert.Equal(ScrollAnchor.IdentityOf(q1), ScrollAnchor.IdentityOf(q2));
        var w1 = new NewsItem("Athens 20°", "", "Athens", DateTimeOffset.UtcNow, "Athens", Kind: NewsItemKind.Weather, Key: "ath");
        Assert.Equal(ScrollAnchor.IdentityOf(w1), ScrollAnchor.IdentityOf(w1 with { Title = "Athens 21°" }));
        var news = new NewsItem("Headline", "https://n.example/1", "S", DateTimeOffset.UtcNow);
        Assert.Equal(ScrollAnchor.IdentityOf(news), ScrollAnchor.IdentityOf(news with { Title = "Headline (updated)" }));
        Assert.NotEqual(ScrollAnchor.IdentityOf(news), ScrollAnchor.IdentityOf(news with { Url = "https://n.example/2" }));
    }
}

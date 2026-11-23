using System.Drawing;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public sealed class P1CanvasAccessibilityTests
{
    [Fact]
    public void CanvasProvidesAccessibleHeadlinesAndSupportsRegionRendering()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var canvas = new TickerCanvas { Size = new Size(900, 48) };
                canvas.SetItems(new[] { new NewsItem("Headline for screen reader",
                    "https://example.org/1","Source",DateTimeOffset.UtcNow) });
                Assert.True(canvas.TabStop);
                Assert.Contains("Headline for screen reader", canvas.AccessibleDescription);
                using var full = canvas.RenderFrame();
                using var partial = canvas.RenderPartialFrameForTest(new Rectangle(830, 0, 40, 48));
                Assert.Equal(1,canvas.PartialFrameCount);
                Assert.Equal(full.Size, partial.Size);
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (error is not null) throw new Exception("Canvas regression failed", error);
    }
}

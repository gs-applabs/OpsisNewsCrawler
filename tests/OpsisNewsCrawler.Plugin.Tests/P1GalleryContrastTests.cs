using System.Drawing;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public sealed class P1GalleryContrastTests
{
    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(255, 255, 255, false)]
    [InlineData(0, 188, 212, false)]
    public void AccentBannerPicksLegibleForeground(int r, int g, int b, bool white)
    {
        var onAccent = GalleryColorContrast.Foreground(Color.FromArgb(r,g,b));
        Assert.Equal(white ? Color.White : Color.Black, onAccent);
        Assert.True(GalleryColorContrast.ContrastRatio(onAccent, Color.FromArgb(r, g, b)) >= 4.5,
            "Breaking headline banner must preserve WCAG AA contrast on the Opsis accent.");
    }

    [Theory]
    [InlineData(96, 100)]
    [InlineData(144, 150)]
    [InlineData(192, 200)]
    [InlineData(384, 400)]
    public void GalleryCardsScaleWithMonitorDpiWithoutCreatingAnotherWindow(int dpi, int pixels)
    {
        Assert.Equal(pixels, GalleryColorContrast.CardHeightForDpi(dpi));
    }
}

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
    }
}

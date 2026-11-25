using System.Drawing;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1DpiLayoutTests
{
    [Theory]
    [InlineData(96, 520)]
    [InlineData(144, 780)]
    [InlineData(192, 1040)]
    public void LogicalPixelScalingMatchesDpi(int dpi, int expected) =>
        Assert.Equal(expected, UiDpiLayout.Pixels(520,dpi));

    [Theory]
    [InlineData(96)]
    [InlineData(144)]
    [InlineData(192)]
    public void LiveVideoPopupClampsToWorkingArea(int dpi)
    {
        var area = new Rectangle(-1600, 0, 1536, 864);
        var anchor = new Rectangle(-420, 800, 400, 44);
        var popup = UiDpiLayout.PopupAt(anchor, area, dpi);
        Assert.True(area.Contains(popup));
        Assert.True(popup.Height > 0 && popup.Width > 0);
    }
}

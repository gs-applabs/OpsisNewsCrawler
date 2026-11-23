using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P2VisualPresetsTests
{
    [Fact]
    public void PresetsAreDistinctAndBoundedAndUseOnlySupportedLookValues()
    {
        Assert.Equal(3,VisualPresetPolicy.All.Count);
        Assert.Equal(3,VisualPresetPolicy.All.Select(x => x.Key).Distinct().Count());
        Assert.All(VisualPresetPolicy.All,p =>
        {
            Assert.InRange(p.Opacity,30,100);
            Assert.Contains(p.Backdrop,new[] { "acrylic","blur","gradient" });
            Assert.Contains(p.ThemeName,new[] { "system","dark","Contrast" });
        });
    }
}

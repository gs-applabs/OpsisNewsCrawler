using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1ContrastVariationTests
{
    [Fact]
    public void UniformDarkBackgroundNeedsNoExtraShadowForWhiteText() =>
        Assert.False(ContrastVariationPolicy.NeedsShadow(0.01,0.08,false));

    [Fact]
    public void MixedWallpaperRequiresExtraShadowForEitherTextChoice()
    {
        Assert.True(ContrastVariationPolicy.NeedsShadow(0.01,0.90,true));
        Assert.True(ContrastVariationPolicy.NeedsShadow(0.01,0.90,false));
    }

    [Fact]
    public void InvalidBackdropDataFailsSafe() =>
        Assert.True(ContrastVariationPolicy.NeedsShadow(double.NaN,0.5,true));
}

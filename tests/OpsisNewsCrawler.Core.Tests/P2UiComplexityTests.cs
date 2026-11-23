using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P2UiComplexityTests
{
    [Theory]
    [InlineData("simple",0)]
    [InlineData("advanced",1)]
    [InlineData("expert",2)]
    [InlineData("unexpected",2)]
    [InlineData(null,2)]
    public void ComplexityModeNormalizesWithoutLosingAdvancedSettings(string? text, int result) =>
        Assert.Equal(result, UiComplexity.Parse(text));

    [Fact]
    public void UnknownSettingsDoNotDeleteExistingValues()
    {
        var c = new AppConfig { UiComplexityMode = "simple", MainSpeedB = 140, BarBackdrop = "blur" };
        Assert.Equal("simple",UiComplexity.ToText(UiComplexity.Parse(c.UiComplexityMode)));
        Assert.Equal(140,c.MainSpeedB);
        Assert.Equal("blur",c.BarBackdrop);
    }
}

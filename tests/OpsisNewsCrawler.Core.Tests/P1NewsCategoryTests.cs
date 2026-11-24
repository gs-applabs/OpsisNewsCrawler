using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1NewsCategoryTests
{
    [Fact]
    public void ExistingCategoryIsOptInAndDoesNotReadAnyLocation()
    {
        var config = new AppConfig();
        config.Normalize();
        Assert.Equal("", WidgetStudio.Resolve(config.WidgetProfiles, "news").NewsCategoryId);
        config.WidgetProfiles["news"] = new WidgetStudioProfile { NewsCategoryId = "Greece" };
        config.Normalize();
        Assert.Equal("Greece", config.WidgetProfiles["news"].NewsCategoryId);
        Assert.NotNull(config.FindCategory("Greece"));
    }

    [Fact]
    public void MissingOrDeletedCategoryRevertsToTheActiveTickerSource()
    {
        var config = new AppConfig();
        config.Normalize();
        config.WidgetProfiles["news"] = new WidgetStudioProfile { NewsCategoryId = "DeletedCategory" };
        config.Normalize();
        Assert.Empty(config.WidgetProfiles["news"].NewsCategoryId);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData(" athens/attica ")]
    [InlineData("https://example.org")]
    [InlineData("GR@GPS")]
    public void RejectArbitraryPathsAndLocationIdentifiers(string input)
    {
        Assert.Equal("", WidgetStudio.CleanCategoryId(input));
    }
}

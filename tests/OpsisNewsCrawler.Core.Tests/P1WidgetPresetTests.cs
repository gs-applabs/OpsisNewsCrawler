using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1WidgetPresetTests
{
    [Theory]
    [InlineData("newsroom")]
    [InlineData("financial")]
    [InlineData("weather-hazards")]
    [InlineData("greece-today")]
    [InlineData("readable")]
    public void TemplatesContainExactlyTheFiveExistingWidgetKinds(string key)
    {
        var profile = WidgetStudioPresets.Create(key);
        Assert.Equal(5, profile.Count);
        Assert.Equal(
            WidgetStudio.Kinds.OrderBy(x => x),
            profile.Keys.OrderBy(x => x));
        Assert.All(profile, kv =>
        {
            Assert.InRange(kv.Value.MaxItems, 3, 30);
            if (kv.Value.Layout == "gallery")
                Assert.True(WidgetStudio.CanUseGallery(kv.Key));
            if (kv.Key == "history") Assert.False(kv.Value.ShowTime);
        });
    }

    [Fact]
    public void TemplatesReturnIndependentValuesAndNeverAlterSavedUserConfig()
    {
        var one = WidgetStudioPresets.Create("newsroom");
        one["news"].MaxItems = 3;
        var another = WidgetStudioPresets.Create("newsroom");
        Assert.Equal(12, another["news"].MaxItems);
        var original = new AppConfig();
        Assert.Empty(original.WidgetProfiles); // opt-in, default UI unchanged
    }

    [Fact]
    public void UnknownTemplateCannotCreateArbitraryWidgetKinds()
    {
        Assert.Throws<ArgumentException>(() => WidgetStudioPresets.Create("../../untrusted"));
    }
}

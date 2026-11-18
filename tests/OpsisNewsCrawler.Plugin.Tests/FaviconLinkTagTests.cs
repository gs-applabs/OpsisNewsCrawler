using System.Reflection;
using System.Text.RegularExpressions;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public class FaviconLinkTagTests
{
    private static Regex LinkTag => (Regex)typeof(FaviconCache).GetField("LinkTag", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void TheLinkTagPatternFindsIconLinksAndHasNoControlCharacters()
    {
        // 0.22.0 stored a real backspace instead of \b, so no <link> tag ever matched
        Assert.DoesNotContain(LinkTag.ToString(), c => char.IsControl(c));
        Assert.True(LinkTag.IsMatch("<link rel=\"icon\" href=\"/favicon.png\">"));
        Assert.False(LinkTag.IsMatch("<linkage>"));
    }
}

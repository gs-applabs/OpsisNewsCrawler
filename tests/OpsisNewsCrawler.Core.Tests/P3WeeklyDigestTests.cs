using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;
public sealed class P3WeeklyDigestTests
{
    [Fact] public void WeeklyDigestOnlyContainsChosenWeekAndEscapesMarkup()
    {
        var when=new DateTimeOffset(2026,11,23,12,0,0,TimeSpan.Zero);
        var items=new[]{
            new NewsItem("<bad>& headline","https://example.org/story","A",when,"<iframe>"),
            new NewsItem("Outside week","https://example.org/old","B",when.AddDays(-9))
        };
        var html=WeeklyDigest.Html(items,new DateOnly(2026,11,23),"Weekly");
        Assert.Contains("&lt;bad&gt;",html);
        Assert.DoesNotContain("<iframe>",html);
        Assert.DoesNotContain("Outside week",html);
    }
}

using System.Text;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1SourceAndDateTests
{
    [Theory]
    [InlineData("bbc.com","bbc.co.uk")]
    [InlineData("ert.gr","ertnews.gr")]
    [InlineData("ap.org","apnews.com")]
    [InlineData("dw.de","dw.com")]
    public void KnownSameNewsroomIsOnePublisherFamily(string one, string two) =>
        Assert.Equal(PublisherIdentity.Canonical(one), PublisherIdentity.Canonical(two));

    [Fact]
    public void UnknownNewsroomsStayIndependent()
    {
        Assert.NotEqual(PublisherIdentity.Canonical("source-one.example"), PublisherIdentity.Canonical("source-two.example"));
    }

    [Fact]
    public void NormalizationPreservesValidUtcDateAndRejectsFutureOrInvalid()
    {
        var now = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(now.AddMinutes(-5), NewsDatePolicy.Normalize(now.AddMinutes(-5).ToOffset(TimeSpan.FromHours(2)), now));
        Assert.Equal(now, NewsDatePolicy.Normalize(now.AddMinutes(5), now));
        Assert.Null(NewsDatePolicy.Normalize(now.AddHours(5), now));
        Assert.Null(NewsDatePolicy.Normalize(new DateTimeOffset(1900,1,1,0,0,0,TimeSpan.Zero), now));
        Assert.Null(NewsDatePolicy.Normalize(null, now));
    }

    [Fact]
    public void RssFutureDatedArticleBecomesUndatedNotBreaking()
    {
        var xml = "<rss><channel><item><title>Important alert</title><link>https://feed.example/story</link><pubDate>Wed, 20 Jan 2100 12:00:00 GMT</pubDate></item></channel></rss>";
        var item = Assert.Single(RssNewsProvider.Parse(Encoding.UTF8.GetBytes(xml), "Example", 5));
        Assert.Equal(DateTimeOffset.MinValue, item.PublishedAt);
        Assert.Empty(new BreakingEngine().Urgent(new[] { item }, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void BreakingDetailsShowAlgorithmicScoreNotTruthProbability()
    {
        var assessment = new BreakingAssessment(new NewsItem("Title","https://example.org","Source",
            DateTimeOffset.UtcNow), 67, true, new[] { new BreakingReason("fresh","1") },
            new[] { "clickbait" }, 2);
        var detailed = BreakingWhy.Detailed(assessment, BreakingSensitivity.Normal);
        Assert.Contains("67/100", detailed);
        Assert.Contains("threshold 58", detailed);
        Assert.Contains("clickbait", detailed);
    }
}

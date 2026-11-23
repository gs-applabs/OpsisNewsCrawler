using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P2ArticleEvidenceTests
{
    [Fact]
    public void ShowsDistinctAdditionalPublishersAndAvoidsMirroredDuplicates()
    {
        var item = new NewsItem("Event","https://bbc.com/story","BBC",DateTimeOffset.UtcNow,
            StoryReports: new[]
            {
                new StoryReport("BBC","bbc.co.uk",DateTimeOffset.UtcNow),
                new StoryReport("Newsroom","example.com",DateTimeOffset.UtcNow),
                new StoryReport("Same Newsroom","www.example.com",DateTimeOffset.UtcNow)
            });
        var result=ArticleEvidenceFormatter.Summary(item,false);
        Assert.Contains("example.com",result);
        Assert.DoesNotContain("bbc.co.uk",result);
        Assert.Contains("do not prove accuracy",result);
    }
}

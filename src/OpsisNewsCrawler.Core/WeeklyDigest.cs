using System.Globalization;
using System.Net;
using System.Text;

namespace OpsisNewsCrawler.Core;

/// <summary>Bounded offline weekly digest, not an automated mailing service.</summary>
public static class WeeklyDigest
{
    public static string Html(IEnumerable<NewsItem> items, DateOnly ending, string heading,
        int maxStories = 200)
    {
        var first = ending.AddDays(-6);
        var news = items.Where(n=>n.Kind==NewsItemKind.News &&
            DateOnly.FromDateTime(n.PublishedAt.LocalDateTime) is var day && day>=first && day<=ending)
            .OrderByDescending(n=>n.PublishedAt).Take(Math.Clamp(maxStories,1,300)).ToArray();
        var sb = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>").Append(WebUtility.HtmlEncode(heading)).Append("</title>")
            .Append("<style>body{font:16px system-ui,sans-serif;max-width:850px;margin:auto;padding:24px;line-height:1.6;color:#192536}article{border-top:1px solid #ddd;padding:12px 0}small{color:#647084}@media print{article{break-inside:avoid}}</style></head><body><h1>")
            .Append(WebUtility.HtmlEncode(heading)).Append("</h1><p>")
            .Append(first.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture)).Append(" – ")
            .Append(ending.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture)).Append("</p>");
        foreach(var story in news)
        {
            sb.Append("<article><small>").Append(WebUtility.HtmlEncode(story.Source)).Append(" · ")
                .Append(story.PublishedAt.ToLocalTime().ToString("dd/MM HH:mm",CultureInfo.InvariantCulture))
                .Append("</small><h2>");
            var allowed=Uri.TryCreate(story.Url,UriKind.Absolute,out var url) &&
                url.Scheme==Uri.UriSchemeHttps && NetGuard.IsPublicHttpUrl(story.Url);
            if(allowed)sb.Append("<a rel=\"noopener noreferrer\" href=\"")
                .Append(WebUtility.HtmlEncode(url!.AbsoluteUri)).Append("\">");
            sb.Append(WebUtility.HtmlEncode(story.Title));
            if(allowed)sb.Append("</a>");
            sb.Append("</h2><p>").Append(WebUtility.HtmlEncode(story.Summary??"")).Append("</p></article>");
        }
        return sb.Append("<footer>Offline archive. Article descriptions are provided by their sources.</footer></body></html>").ToString();
    }
}

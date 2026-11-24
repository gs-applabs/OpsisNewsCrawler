using System.Globalization;
using System.Net;
using System.Text;

namespace OpsisNewsCrawler.Core;

/// <summary>Entirely offline HTML digest, suitable for printing to PDF via the user's browser.</summary>
public static class NewsDigest
{
    public static string Html(IEnumerable<NewsItem> items, DateOnly day, string title, int limit = 120)
    {
        var rows = items.Where(n => n.Kind == NewsItemKind.News &&
                                    DateOnly.FromDateTime(n.PublishedAt.LocalDateTime) == day)
            .OrderByDescending(n => n.PublishedAt).Take(Math.Clamp(limit, 1, 300)).ToArray();
        var h = new StringBuilder("<!doctype html><html lang=\"el\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title>")
            .Append("<style>body{font:16px system-ui,sans-serif;max-width:800px;margin:2rem auto;padding:0 1rem;line-height:1.55;color:#192536}h1{border-bottom:2px solid #1472a6;padding-bottom:.4em}article{border-bottom:1px solid #ddd;padding:1em 0}.meta{color:#536475;font-size:.86em}a{color:#125d92}@media print{body{margin:0;max-width:none}article{break-inside:avoid}}</style></head><body><h1>")
            .Append(WebUtility.HtmlEncode(title)).Append("</h1><p>").Append(day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
            .Append(" · ").Append(rows.Length.ToString(CultureInfo.InvariantCulture)).Append("</p>");
        foreach (var item in rows)
        {
            var safeTitle = WebUtility.HtmlEncode(item.Title);
            h.Append("<article><div class=\"meta\">").Append(WebUtility.HtmlEncode(item.Source))
                .Append(" · ").Append(item.PublishedAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture))
                .Append("</div><h2>");
            if (Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && NetGuard.IsPublicHttpUrl(item.Url))
                h.Append("<a rel=\"noopener noreferrer\" href=\"").Append(WebUtility.HtmlEncode(uri.AbsoluteUri)).Append("\">");
            h.Append(safeTitle);
            if (Uri.TryCreate(item.Url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps && NetGuard.IsPublicHttpUrl(item.Url))
                h.Append("</a>");
            h.Append("</h2><p>").Append(WebUtility.HtmlEncode(item.Summary ?? "")).Append("</p></article>");
        }
        return h.Append("<footer>Offline news digest · Sources remain responsible for their reporting.</footer></body></html>").ToString();
    }

    public static void SaveHtml(string filePath, IEnumerable<NewsItem> items, DateOnly day, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(folder);
        // User-initiated export only, never background writes.
        SafeFile.WriteAllText(filePath, Html(items, day, title));
    }
}

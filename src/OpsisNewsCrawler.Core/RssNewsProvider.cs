using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OpsisNewsCrawler.Core;

/// <summary>The answer of <see cref="RssNewsProvider.TestAsync"/>.</summary>
public sealed record FeedTestResult(bool Ok, int Count, DateTimeOffset? Newest, string Error);

/// <summary>Health of one feed URL: last success, last attempt, failures in a row and the last error message.</summary>
public sealed record FeedHealth(string Url, DateTimeOffset? LastOk, DateTimeOffset? LastTry, int ConsecutiveFailures, string? LastError, DateTimeOffset? NextRetryAt = null, DateTimeOffset? NewestItemAt = null);

/// <summary>
/// Downloads and parses RSS 2.0 / Atom feeds. A failing feed never breaks the others.
/// </summary>
public sealed class RssNewsProvider : IDisposable
{
    private const long MaxFeedBytes = 5 * 1024 * 1024;
    public const int MaxConcurrentFeeds = 6;
    public const int MaxConcurrentPerHost = 2;
    private int activeRequests;
    private int peakRequests;
    /// <summary>Peak concurrent feed downloads since construction, for diagnostics.</summary>
    public int PeakConcurrentRequests => Volatile.Read(ref peakRequests);
    private long downloadedBytes;
    /// <summary>Approximate successful RSS bytes received by this instance during its lifetime.</summary>
    public long DownloadedBytes => Interlocked.Read(ref downloadedBytes);
    private const int MaxTitleLength = 200;
    private const int MaxSummaryLength = 400;

    private readonly HttpClient http;
    private readonly object failLock = new();
    private List<string> failedFeeds = new();

    /// <summary>Names of the feeds that could not be downloaded in the last <see cref="FetchAsync"/> (network or server problem).</summary>
    public IReadOnlyList<string> LastFailedFeeds
    {
        get { lock (failLock) return failedFeeds.ToList(); }
    }

    private const int CachedItemsPerFeed = 50;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, FeedState> states = new(StringComparer.OrdinalIgnoreCase);

    private sealed class FeedState
    {
        public string? ETag;
        public string? LastModified;
        public List<NewsItem>? Items;
        public DateTimeOffset? LastOk;
        public DateTimeOffset? LastTry;
        public int ConsecutiveFailures;
        public string? LastError;
        public DateTimeOffset? NextRetryAt;

        public void MarkOk()
        {
            lock (this) { LastOk = LastTry = DateTimeOffset.UtcNow; ConsecutiveFailures = 0; LastError = null; NextRetryAt = null; }
        }
        public void MarkFailed(string error, TimeSpan? retryAfter = null)
        {
            lock (this)
            {
                LastTry = DateTimeOffset.UtcNow;
                ConsecutiveFailures = Math.Min(ConsecutiveFailures + 1, 30);
                LastError = error;
                NextRetryAt = LastTry.Value + FeedRetryPolicy.Delay(ConsecutiveFailures, retryAfter);
            }
        }
        public bool CoolingDown => NextRetryAt is { } retry && retry > DateTimeOffset.UtcNow;
    }

    /// <summary>How a source did in the last fetches (null times = never tried / never worked). For a "source health" display.</summary>
    public IReadOnlyList<FeedHealth> Health() => states.Select(kv =>
    {
        var s = kv.Value;
        lock (s) return new FeedHealth(kv.Key, s.LastOk, s.LastTry, s.ConsecutiveFailures, s.LastError, s.NextRetryAt,
            s.Items is { Count: > 0 } items ? items.Max(i => i.PublishedAt) : null);
    }).OrderBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToList();

    private void RecordFailure(string name)
    {
        lock (failLock) failedFeeds.Add(name);
    }

    public RssNewsProvider(HttpMessageHandler? handler = null)
    {
        http = new HttpClient(handler ?? new RedirectGuardHandler());
        http.Timeout = TimeSpan.FromSeconds(15);
        http.MaxResponseContentBufferSize = MaxFeedBytes;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpsisNewsCrawler/0.28.1)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml, */*");
    }

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(
        IEnumerable<FeedSource> sources, int maxPerFeed, CancellationToken cancellationToken = default,
        int maxAgeHours = 168, bool newestFirst = false)
    {
        lock (failLock) failedFeeds = new();
        using var global = new SemaphoreSlim(MaxConcurrentFeeds);
        var domainGates = new System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        async Task<List<NewsItem>> FetchLimitedAsync(FeedSource source)
        {
            await global.WaitAsync(cancellationToken).ConfigureAwait(false);
            var hostname = Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) ? uri.Host : source.Url;
            var domain = domainGates.GetOrAdd(hostname, _ => new SemaphoreSlim(MaxConcurrentPerHost));
            var acquired = false;
            try
            {
                await domain.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;
                var active = Interlocked.Increment(ref activeRequests);
                int previous;
                while ((previous = Volatile.Read(ref peakRequests)) < active &&
                       Interlocked.CompareExchange(ref peakRequests, active, previous) != previous) { }
                try { return await FetchOneAsync(source, maxPerFeed, cancellationToken).ConfigureAwait(false); }
                finally { Interlocked.Decrement(ref activeRequests); }
            }
            finally { if (acquired) domain.Release(); global.Release(); }
        }

        var feeds = sources.Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .DistinctBy(s => s.Url, StringComparer.OrdinalIgnoreCase).ToList();
        var perFeed = await Task.WhenAll(feeds.Select(FetchLimitedAsync)).ConfigureAwait(false);
        foreach (var gate in domainGates.Values) gate.Dispose();

        // Round-robin, so every source is visible in the ticker instead of the busiest one winning.
        var result = new List<NewsItem>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenWords = new List<(HashSet<string> Words, int Index)>();
        var titleIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // The publishers whose copy of a story was folded into the kept one: the urgent-news engine counts them as confirmations.
        var folded = new Dictionary<int, List<StoryReport>>();
        void Fold(int index, NewsItem copy)
        {
            if (copy.PublishedAt == DateTimeOffset.MinValue) return;
            var host = Uri.TryCreate(copy.Url, UriKind.Absolute, out var u) ? u.Host : "";
            if (!folded.TryGetValue(index, out var list)) folded[index] = list = new List<StoryReport>();
            if (list.Count < 12) list.Add(new StoryReport(copy.Source, host, copy.PublishedAt));
        }
        for (var i = 0; i < maxPerFeed; i++)
        {
            foreach (var feed in perFeed)
            {
                if (i >= feed.Count) continue;
                var item = feed[i];
                var titleKey = NormalizeTitle(item.Title);
                if (!seenUrls.Add(CanonicalizeUrl(item.Url))) continue;
                // Different publishers frequently syndicate the same story with different URLs.
                if (titleKey.Length >= 18)
                {
                    if (titleIndex.TryGetValue(titleKey, out var sameTitle)) { Fold(sameTitle, item); continue; }
                    titleIndex[titleKey] = result.Count;
                }
                // ...and with different wording: the same story from another source (round-robin keeps the first source's version).
                var words = HeadlineDedup.Words(item.Title);
                var match = seenWords.FirstOrDefault(w => HeadlineDedup.AreSameStory(w.Words, words));
                if (match.Words is not null) { Fold(match.Index, item); continue; }
                seenWords.Add((words, result.Count));
                result.Add(item);
            }
        }
        foreach (var (index, reports) in folded) result[index] = result[index] with { StoryReports = reports };
        return FilterAndOrder(result, maxAgeHours, newestFirst, DateTimeOffset.UtcNow);
    }

    /// <summary>Applies the age limit and optional chronological ordering. Undated items are kept at the end.</summary>
    public static IReadOnlyList<NewsItem> FilterAndOrder(IEnumerable<NewsItem> items, int maxAgeHours,
        bool newestFirst, DateTimeOffset now)
    {
        var cutoff = now.AddHours(-Math.Clamp(maxAgeHours, 1, 720));
        var result = items.Where(item => item.PublishedAt == DateTimeOffset.MinValue || item.PublishedAt >= cutoff).ToList();
        if (newestFirst)
            result = result.OrderByDescending(item => item.PublishedAt == DateTimeOffset.MinValue
                ? DateTimeOffset.MinValue : item.PublishedAt).ToList();
        return result;
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        var decoded = WebUtility.HtmlDecode(title);
        return Regex.Replace(decoded, @"[^\p{L}\p{N}]+", " ").Trim().ToLowerInvariant();
    }

    private static string CanonicalizeUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return value?.Trim() ?? string.Empty;
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        foreach (var key in query.AllKeys.Where(k => k is not null &&
                     (k.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
                      k.Equals("fbclid", StringComparison.OrdinalIgnoreCase) ||
                      k.Equals("gclid", StringComparison.OrdinalIgnoreCase))).ToList()) query.Remove(key);
        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    /// <summary>Downloads one feed and says whether it works: number of stories and the newest one.</summary>
    public async Task<FeedTestResult> TestAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaxFeedBytes) return new FeedTestResult(false, 0, null, "feed too large");
            var bytes = await ReadBoundedAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), MaxFeedBytes, ct).ConfigureAwait(false);
            Interlocked.Add(ref downloadedBytes, bytes.LongLength);
            var items = Parse(bytes, "", 50);
            return items.Count == 0
                ? new FeedTestResult(false, 0, null, "no stories found")
                : new FeedTestResult(true, items.Count, items.Max(i => i.PublishedAt), "");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException or IOException or InvalidOperationException or UriFormatException or InvalidDataException)
        {
            return new FeedTestResult(false, 0, null, ex.Message);
        }
    }

    private async Task<List<NewsItem>> FetchOneAsync(FeedSource source, int maxItems, CancellationToken ct)
    {
        var state = states.GetOrAdd(source.Url, _ => new FeedState());
        lock (state)
            if (state.CoolingDown) return state.Items?.Take(maxItems).ToList() ?? new List<NewsItem>();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
            // Conditional GET: an unchanged feed costs a few hundred bytes and no parsing.
            if (state.ETag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", state.ETag);
            if (state.LastModified is not null) request.Headers.TryAddWithoutValidation("If-Modified-Since", state.LastModified);
            using var response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified && state.Items is not null)
            {
                state.MarkOk();
                return state.Items.Take(maxItems).ToList();
            }
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                var retry = response.Headers.RetryAfter?.Delta;
                if (retry is null && response.Headers.RetryAfter?.Date is { } retryDate)
                    retry = retryDate - DateTimeOffset.UtcNow;
                state.MarkFailed($"HTTP {(int)response.StatusCode}", retry);
                RecordFailure(source.Name);
                return new List<NewsItem>();
            }
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaxFeedBytes)
                throw new InvalidDataException("feed exceeds the 5 MB size limit");
            var bytes = await ReadBoundedAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), MaxFeedBytes, ct).ConfigureAwait(false);
            Interlocked.Add(ref downloadedBytes, bytes.LongLength);

            var parsed = Parse(bytes, source.Name, Math.Max(maxItems, CachedItemsPerFeed));
            state.ETag = response.Headers.ETag?.ToString();
            state.LastModified = response.Content.Headers.LastModified?.ToString("R");
            state.Items = parsed;
            state.MarkOk();
            return parsed.Take(maxItems).ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException or IOException or InvalidDataException or UriFormatException or ArgumentException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"OpsisNewsCrawler: feed '{source.Name}' failed: {ex.Message}");
            RecordFailure(source.Name);
            state.MarkFailed(ex.Message);
            return new List<NewsItem>();
        }
    }

    /// <summary>Streams at most the allowed byte count, even when servers use chunked encoding or lie about Content-Length.</summary>
    public static async Task<byte[]> ReadBoundedAsync(Stream stream, long maxBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        using var buffer = new MemoryStream();
        var bytes = new byte[16 * 1024];
        while (true)
        {
            var n = await stream.ReadAsync(bytes.AsMemory(), ct).ConfigureAwait(false);
            if (n == 0) return buffer.ToArray();
            if (buffer.Length + n > maxBytes) throw new InvalidDataException($"feed exceeds the {maxBytes} byte size limit");
            buffer.Write(bytes, 0, n);
        }
    }

    public static List<NewsItem> Parse(byte[] xml, string sourceName, int maxItems)
    {
        ArgumentNullException.ThrowIfNull(xml);
        if (xml.LongLength > MaxFeedBytes) throw new InvalidDataException("RSS payload exceeds the 5 MB limit");
        if (maxItems <= 0) return new List<NewsItem>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CheckCharacters = false,
            MaxCharactersFromEntities = 1024,
            MaxCharactersInDocument = MaxFeedBytes * 3
        };

        XDocument doc;
        using (var stream = new MemoryStream(xml))
        using (var reader = XmlReader.Create(stream, settings))
        {
            doc = XDocument.Load(reader);
        }

        var items = new List<NewsItem>();
        var entries = doc.Descendants().Where(e => e.Name.LocalName is "item" or "entry");
        foreach (var entry in entries)
        {
            var title = Clean(ChildValue(entry, "title"));
            var link = FindLink(entry);
            if (string.IsNullOrEmpty(title) || link is null) continue;

            if (title.Length > MaxTitleLength) title = title[..MaxTitleLength].TrimEnd() + "…";
            var date = NewsDatePolicy.Normalize(ParseDate(ChildValue(entry, "pubDate") ?? ChildValue(entry, "published")
                                 ?? ChildValue(entry, "updated") ?? ChildValue(entry, "date")), DateTimeOffset.UtcNow);
            var summary = ExtractSummary(entry, title);
            var image = ExtractImage(entry);
            items.Add(new NewsItem(title, link, sourceName, date ?? DateTimeOffset.MinValue, summary, image));
        }

        // Stable sort: items without a date keep the order of the feed.
        return items.OrderByDescending(i => i.PublishedAt).Take(maxItems).ToList();
    }

    private static string? ExtractSummary(XElement entry, string title)
    {
        var html = ChildValue(entry, "description") ?? ChildValue(entry, "summary")
                   ?? ChildValue(entry, "encoded") ?? ChildValue(entry, "content");
        if (string.IsNullOrWhiteSpace(html)) return null;

        var text = Clean(Regex.Replace(html, "<[^>]+>", " "));
        // WordPress feeds append "The post X appeared first on Y."
        text = Regex.Replace(text, @"\s*The post .* appeared first on .*$", "", RegexOptions.IgnoreCase).Trim();
        if (text.Length == 0 || string.Equals(text, title, StringComparison.OrdinalIgnoreCase)) return null;
        return text.Length > MaxSummaryLength ? text[..MaxSummaryLength].TrimEnd() + "…" : text;
    }

    private static string? ExtractImage(XElement entry)
    {
        foreach (var e in entry.Descendants())
        {
            var name = e.Name.LocalName;
            if (name is not ("enclosure" or "thumbnail" or "content")) continue;
            var url = (string?)e.Attribute("url");
            if (string.IsNullOrWhiteSpace(url)) continue;

            var type = (string?)e.Attribute("type");
            var medium = (string?)e.Attribute("medium");
            var isImage = name == "thumbnail"
                          || medium == "image"
                          || (type is not null && type.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                          || (type is null && LooksLikeImage(url));
            if (isImage && ValidUrl(url) is { } valid) return valid;
        }

        var html = ChildValue(entry, "encoded") ?? ChildValue(entry, "description")
                   ?? ChildValue(entry, "summary") ?? ChildValue(entry, "content");
        if (string.IsNullOrEmpty(html)) return null;
        var m = Regex.Match(html, "<img[^>]+src=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        return m.Success ? ValidUrl(WebUtility.HtmlDecode(m.Groups[1].Value)) : null;
    }

    private static bool LooksLikeImage(string url)
    {
        var path = url.Split('?', '#')[0];
        return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static string? FindLink(XElement entry)
    {
        foreach (var l in entry.Elements().Where(e => e.Name.LocalName == "link"))
        {
            var rel = (string?)l.Attribute("rel");
            if (rel is not null && rel != "alternate") continue;
            var candidate = (string?)l.Attribute("href");
            if (string.IsNullOrWhiteSpace(candidate)) candidate = l.Value;
            var url = ValidUrl(candidate);
            if (url is not null) return url;
        }
        return ValidUrl(ChildValue(entry, "guid"));
    }

    private static string? ValidUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
    }

    private static DateTimeOffset? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal;
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, styles, out var value)) return value;

        // Accept "+0300" style offsets that some Greek feeds use after an ISO-like date.
        var fixedText = Regex.Replace(text, @"\s*([+-]\d{2})(\d{2})$", " $1:$2");
        return DateTimeOffset.TryParse(fixedText, CultureInfo.InvariantCulture, styles, out value) ? value : null;
    }

    public void Dispose() => http.Dispose();
}

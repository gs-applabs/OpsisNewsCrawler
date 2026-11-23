using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>An explicitly imported or manually entered economic event, not a live verified release feed.</summary>
public sealed record EconomicEvent(string Id, string Title, string Country, DateTimeOffset StartsAt,
    string Source, string? Url = null, int Impact = 0, string? Actual = null, string? Forecast = null,
    string? Previous = null);

public static class EconomicCalendar
{
    public static IReadOnlyList<EconomicEvent> Upcoming(IEnumerable<EconomicEvent> entries,
        DateTimeOffset now, int days = 14) =>
        entries.Where(x=>x.StartsAt >= now && x.StartsAt <= now.AddDays(Math.Clamp(days,1,90)))
        .Where(x=>!string.IsNullOrWhiteSpace(x.Title)&&x.Impact is >=0 and <=3)
        .OrderBy(x=>x.StartsAt).ThenByDescending(x=>x.Impact).Take(300).ToArray();

    /// <summary>Import fixed JSON data from a trusted file; no network or embedded scripts.</summary>
    public static IReadOnlyList<EconomicEvent> ImportJson(string json)
    {
        if(json.Length>1_000_000)throw new InvalidDataException("Calendar exceeds 1 MB.");
        var data=JsonSerializer.Deserialize<List<EconomicEvent>>(json) ?? new();
        if(data.Count>2000)throw new InvalidDataException("Too many events.");
        return data.Where(x=>x is not null && x.Id.Length is >0 and <=100 &&
            x.Title.Length is >0 and <=300 && x.Impact is >=0 and <=3).Take(2000).ToArray();
    }

    public static string ExportIcs(IEnumerable<EconomicEvent> entries)
    {
        static string Clean(string s)=>s.Replace("\\","\\\\").Replace(";","\\;")
            .Replace(",","\\,").Replace("\r"," ").Replace("\n","\\n");
        var sb=new StringBuilder("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Opsis//Crawler//EN\r\nCALSCALE:GREGORIAN\r\n");
        foreach(var x in entries.Take(1000))
        {
            var id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(x.Id)))[..20].ToLowerInvariant();
            sb.Append("BEGIN:VEVENT\r\nUID:").Append(id).Append("@opsis.local\r\nDTSTAMP:")
                .Append(DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'",CultureInfo.InvariantCulture))
                .Append("\r\nDTSTART:").Append(x.StartsAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'",CultureInfo.InvariantCulture))
                .Append("\r\nSUMMARY:").Append(Clean(x.Title)).Append("\r\nDESCRIPTION:")
                .Append(Clean(x.Source)).Append("\r\nEND:VEVENT\r\n");
        }
        return sb.Append("END:VCALENDAR\r\n").ToString();
    }
}

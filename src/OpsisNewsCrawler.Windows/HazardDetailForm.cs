using Microsoft.Web.WebView2.Core;
using OpsisNewsCrawler.Core;
using System.Globalization;
using System.Net;

namespace OpsisNewsCrawler.Windows;

/// <summary>Hazard content and explicit listed-link policy over the common map window.</summary>
internal sealed class HazardDetailForm : MapDetailForm
{
    private readonly HashSet<string> allowedLinks;

    public HazardDetailForm(HazardEvent hazard, Rectangle anchorBar)
        : base(BuildHtml(hazard, DateTimeOffset.Now), Loc.T("app.name") + " — " + HazardText.KindName(hazard.Kind),
            HazardText.Tip(hazard), anchorBar, 600, 420,
            ex => ex is WebView2RuntimeNotFoundException ? Loc.T("live.noRuntime") :
                Loc.T("live.error", ex.GetType().Name + ": " + ex.Message))
    {
        allowedLinks = Links(hazard).Select(l => l.Url).ToHashSet(StringComparer.Ordinal);
    }

    internal override bool AllowsExternalLink(string? url) =>
        url is not null && allowedLinks.Contains(url) && IsWebLink(url);

    /// <summary>The links of an event: one per organisation, then the official page of the first one when it is not among them.</summary>
    internal static IReadOnlyList<(string Source, string Url)> Links(HazardEvent e)
    {
        var list = new List<(string, string)>();
        if (e.SourceUrls is not null)
            foreach (var (source, url) in e.SourceUrls)
                if (IsWebLink(url)) list.Add((source, url));
        if (list.Count == 0 && IsWebLink(e.Url)) list.Add((e.Sources.FirstOrDefault() ?? "", e.Url));
        return list;
    }

    internal static bool IsWebLink(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.UserInfo);

    /// <summary>The page: the map centred on the event with a box of the details. Every text is HTML-encoded, every link is one of the event's own https addresses.</summary>
    internal static string BuildHtml(HazardEvent e, DateTimeOffset now)
    {
        string H(string s) => WebUtility.HtmlEncode(s);
        var culture = CultureInfo.GetCultureInfo(Loc.IsGreek ? "el-GR" : "en-GB");
        var rows = new List<string>();
        var heading = HazardText.KindName(e.Kind) + (string.IsNullOrWhiteSpace(e.Place) ? "" : " — " + e.Place);
        if (e.Magnitude is { } m)
        {
            var text = "M" + m.ToString("0.0", culture);
            if (e.SourceCount > 1 && e.MagnitudeMin is { } lo && e.MagnitudeMax is { } hi && hi - lo >= 0.15)
                text += "  (" + H(Loc.T("hazard.range", lo.ToString("0.0", culture), hi.ToString("0.0", culture), e.SourceCount)) + ")";
            rows.Add("<b>" + H(Loc.T("hazard.detail.magnitude")) + ":</b> " + text);
        }
        if (e.DepthKm is { } d && d > 0) rows.Add("<b>" + H(Loc.T("hazard.detail.depth")) + ":</b> " + Math.Round(d).ToString("0", culture) + " km");
        rows.Add("<b>" + H(Loc.T("hazard.detail.time")) + ":</b> " + e.Time.ToLocalTime().ToString("dd/MM/yyyy HH:mm", culture));
        rows.Add("<b>" + H(Loc.T("hazard.detail.level")) + ":</b> <span class='lv" + Math.Clamp(e.Level, 1, 3) + "'>" + H(Loc.T("hazard.level." + Math.Clamp(e.Level, 1, 3))) + "</span>");
        rows.Add("<b>" + H(Loc.T("hazard.detail.position")) + ":</b> " + e.Lat.ToString("0.00", CultureInfo.InvariantCulture) + ", " + e.Lon.ToString("0.00", CultureInfo.InvariantCulture)
                 + " · " + H(Continents.Name(Continents.Of(e.Lat, e.Lon))));
        var links = Links(e);
        var reported = string.Join(", ", e.Sources.Select(s => links.FirstOrDefault(l => l.Source == s) is { Url: { Length: > 0 } url } ? "<a href='" + H(url) + "'>" + H(s) + "</a>" : H(s)));
        rows.Add("<b>" + H(Loc.T("hazard.detail.reported")) + ":</b> " + reported);
        if (links.Count > 0) rows.Add("<a href='" + H(links[0].Url) + "'>" + H(Loc.T("hazard.detail.official")) + " ↗</a>");

        var lat = e.Lat.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = e.Lon.ToString("0.####", CultureInfo.InvariantCulture);
        var dark = Theme.IsDark;
        var color = e.Level >= 3 ? "#d62828" : e.Level == 2 ? "#e67e22" : "#e0a020";
        var offline = Loc.T("pk.the.map.needs.an");
        return @"<!DOCTYPE html><html><head><meta charset='utf-8'><meta name='referrer' content='origin-when-cross-origin'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' integrity='sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=' crossorigin=''>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js' integrity='sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=' crossorigin=''></script>
<style>
html,body{margin:0;height:100%;font-family:'Segoe UI',sans-serif;background:#1e1e1e}
#map{position:absolute;inset:0;color:#ddd;display:flex;align-items:center;justify-content:center}
.box{position:absolute;top:12px;left:12px;z-index:1000;padding:10px 14px;border-radius:8px;font-size:13px;line-height:1.55;max-width:70%;
background:" + (dark ? "rgba(30,30,34,.93)" : "rgba(255,255,255,.96)") + ";color:" + (dark ? "#eee" : "#222") + @";box-shadow:0 3px 10px rgba(0,0,0,.35)}
.box h3{margin:0 0 6px 0;font-size:15px}a{color:" + (dark ? "#5ec8e5" : "#0b6e99") + @"}
.lv1{color:#e0a020;font-weight:600}.lv2{color:#e67e22;font-weight:600}.lv3{color:#d62828;font-weight:600}
</style></head><body>
<div id='map'>" + H(offline) + @"</div>
<div class='box'><h3>" + H(heading) + "</h3>" + string.Join("<br>", rows) + @"</div>
<script>
if (typeof L !== 'undefined') {
  document.getElementById('map').textContent = '';
  var map = L.map('map').setView([" + lat + ", " + lon + @"], 7);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {maxZoom: 18, attribution: '&copy; OpenStreetMap contributors'}).addTo(map);
  L.circleMarker([" + lat + ", " + lon + "], {radius: 11, color: '" + color + @"', fillColor: '" + color + @"', fillOpacity: .55, weight: 3}).addTo(map);
}
</script></body></html>";
    }

}

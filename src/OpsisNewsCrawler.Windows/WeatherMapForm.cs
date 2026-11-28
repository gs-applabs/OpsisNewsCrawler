using Microsoft.Web.WebView2.Core;
using OpsisNewsCrawler.Core;
using System.Globalization;
using System.Net;

namespace OpsisNewsCrawler.Windows;

/// <summary>Weather content over the common map window; external navigation remains denied.</summary>
internal sealed class WeatherMapForm : MapDetailForm
{
    public WeatherMapForm(GreekCity city, WeatherReading? reading, bool stored, Rectangle anchorBar)
        : base(BuildHtml(city, Loc.IsGreek ? city.NameEl : city.NameEn, reading, stored),
            Loc.T("app.name") + " — " + (Loc.IsGreek ? city.NameEl : city.NameEn),
            (Loc.IsGreek ? city.NameEl : city.NameEn) + "  ·  " + Loc.T("pk.weather.and.map"),
            anchorBar, 560, 380,
            ex => ex is WebView2RuntimeNotFoundException ? Loc.T("pk.microsoft.edge.webview2.runtime") :
                Loc.T("pk.the.map.could.not") + ex.GetType().Name + ": " + ex.Message)
    { }

    /// <summary>The page: a Leaflet map centred on the city with a box of the current weather. Every text is HTML-encoded.</summary>
    internal static string BuildHtml(GreekCity city, string name, WeatherReading? r, bool stored)
    {
        var greek = Loc.IsGreek;
        var culture = CultureInfo.GetCultureInfo("el-GR");
        string H(string s) => WebUtility.HtmlEncode(s);
        var rows = new List<string>();
        if (r is null)
        {
            rows.Add(H(Loc.T("pk.no.weather.data.yet")));
        }
        else
        {
            var kind = WeatherInfo.KindOf(r.Code);
            rows.Add("<b>" + H(WeatherInfo.KindName(kind, greek)) + "</b>  " + r.TemperatureC.ToString("0.0", culture) + " °C");
            if (r.FeelsLikeC is { } feels) rows.Add(H(Loc.T("weather.feels")) + ": " + feels.ToString("0.0", culture) + " °C");
            rows.Add(H(Loc.T("weather.wind")) + ": " + r.WindKmh.ToString("0", culture) + " km/h " + H(Loc.T(new[] { "wind.n", "wind.ne", "wind.e", "wind.se", "wind.s", "wind.sw", "wind.w", "wind.nw" }[WeatherInfo.CompassIndex(r.WindFromDegrees)])));
            if (r.GustsKmh is { } gusts) rows.Add(H(Loc.T("weather.gusts")) + ": " + gusts.ToString("0", culture) + " km/h");
            rows.Add(H(Loc.T("weather.humidity")) + ": " + r.HumidityPercent + "%");
            if (r.PrecipitationMm is { } rain) rows.Add(H(Loc.T("weather.rain")) + ": " + rain.ToString("0.0", culture) + " mm");
            if (r.CloudPercent is { } cloud) rows.Add(H(Loc.T("weather.cloud")) + ": " + cloud + "%");
            if (r.PressureHpa is { } pressure) rows.Add(H(Loc.T("weather.pressure")) + ": " + pressure.ToString("0", culture) + " hPa");
            rows.Add("<span class='muted'>" + H(Loc.T("pk.data.time")) + ": " + r.At.ToLocalTime().ToString("dd/MM HH:mm", culture) + (stored ? " · " + H(Loc.T("pk.stored.data")) : "") + "</span>");
        }
        var lat = city.Lat.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = city.Lon.ToString("0.####", CultureInfo.InvariantCulture);
        var dark = Theme.IsDark;
        var offline = Loc.T("pk.the.map.needs.an");
        return @"<!DOCTYPE html><html><head><meta charset='utf-8'><meta name='referrer' content='origin-when-cross-origin'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' integrity='sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=' crossorigin=''>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js' integrity='sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=' crossorigin=''></script>
<style>
html,body{margin:0;height:100%;font-family:'Segoe UI',sans-serif;background:#1e1e1e}
#map{position:absolute;inset:0;color:#ddd;display:flex;align-items:center;justify-content:center}
.box{position:absolute;top:12px;left:12px;z-index:1000;padding:10px 14px;border-radius:8px;font-size:13px;line-height:1.5;max-width:60%;
background:" + (dark ? "rgba(30,30,34,.92)" : "rgba(255,255,255,.95)") + ";color:" + (dark ? "#eee" : "#222") + @";box-shadow:0 3px 10px rgba(0,0,0,.35)}
.box h3{margin:0 0 4px 0;font-size:15px}.muted{opacity:.65;font-size:11px}
</style></head><body>
<div id='map'>" + H(offline) + @"</div>
<div class='box'><h3>" + H(name) + "</h3>" + string.Join("<br>", rows) + @"</div>
<script>
if (typeof L !== 'undefined') {
  document.getElementById('map').textContent = '';
  var map = L.map('map').setView([" + lat + ", " + lon + @"], 11);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {maxZoom: 18, attribution: '&copy; OpenStreetMap contributors'}).addTo(map);
  L.marker([" + lat + ", " + lon + @"]).addTo(map);
}
</script></body></html>";
    }

}

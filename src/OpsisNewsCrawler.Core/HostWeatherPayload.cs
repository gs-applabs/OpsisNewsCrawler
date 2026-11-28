using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Validates the normalized cached weather JSON from the Opsis host.
/// No network calls, no geolocation, no provider-dependent implicit defaults.
/// </summary>
public static class HostWeatherPayload
{
    public static WeatherReading? Parse(string cityId, string? json, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(cityId) || string.IsNullOrWhiteSpace(json) || json.Length > 32_768)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True ||
                (root.TryGetProperty("stale", out var stale) && stale.ValueKind == JsonValueKind.True))
                return null;
            if (!TryNumber(root, "temp", out var temp) || temp is < -100 or > 70 ||
                !TryNumber(root, "wind", out var wind) || wind is < 0 or > 400 ||
                !TryNumber(root, "code", out var code) || code is < 0 or > 99)
                return null;
            if (!root.TryGetProperty("observedAt", out var observed) ||
                observed.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(observed.GetString(), out var at) ||
                at > now.AddMinutes(5) || now - at > TimeSpan.FromMinutes(30))
                return null;
            var humidity = TryNumber(root, "humidity", out var hum) && hum is >= 0 and <= 100
                ? (int)Math.Round(hum) : 0;
            var dir = TryNumber(root, "windDir", out var dirVal) && dirVal is >= 0 and <= 360
                ? (int)Math.Round(dirVal) : 0;
            var day = root.TryGetProperty("isDay", out var d) && d.ValueKind == JsonValueKind.True;
            string? source = root.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() : "Opsis WeatherHub";
            if (source?.Length > 64) source = source[..64];
            return new WeatherReading(cityId, temp, humidity, wind, dir, (int)code, day, at,
                Source: source);
        }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
    }

    private static bool TryNumber(JsonElement root, string name, out double value)
    {
        value = 0;
        return root.TryGetProperty(name, out var el) &&
               el.ValueKind == JsonValueKind.Number &&
               el.TryGetDouble(out value) && double.IsFinite(value);
    }

    /// <summary>Only missing requested cities need the fallback provider. Ignore unrelated or duplicate results.</summary>
    public static string[] MissingCities(IEnumerable<string> requested, IEnumerable<WeatherReading> shared)
    {
        var have = shared.Select(r => r.CityId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requested.Distinct(StringComparer.OrdinalIgnoreCase).Where(id => !have.Contains(id)).ToArray();
    }

    /// <summary>Prefer shared fresh observations, then fallback, then retain previous observations for requested cities only.</summary>
    public static IReadOnlyList<WeatherReading> Merge(IEnumerable<string> requested,
        IEnumerable<WeatherReading> shared, IEnumerable<WeatherReading> fallback, IEnumerable<WeatherReading> previous)
    {
        var byCity = shared.Concat(fallback).Concat(previous)
            .GroupBy(r => r.CityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return requested.Distinct(StringComparer.OrdinalIgnoreCase).Where(byCity.ContainsKey).Select(id => byCity[id]).ToArray();
    }
}

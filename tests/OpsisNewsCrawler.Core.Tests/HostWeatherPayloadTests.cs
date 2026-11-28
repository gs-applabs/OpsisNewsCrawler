using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class HostWeatherPayloadTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ParsesValidCachedOpsisWeatherWithoutLocationOrNetwork()
    {
        const string json = """{"ok":true,"temp":20.4,"wind":11.5,"code":2,"windDir":210,"humidity":65,"isDay":true,"source":"Open-Meteo","observedAt":"2026-11-24T11:57:00Z"}""";
        var w = HostWeatherPayload.Parse("athens", json, Now);
        Assert.NotNull(w);
        Assert.Equal("athens", w.CityId);
        Assert.Equal(20.4, w.TemperatureC);
        Assert.Equal(65, w.HumidityPercent);
        Assert.Equal("Open-Meteo", w.Source);
    }

    [Theory]
    [InlineData("""{"ok":false,"temp":20,"wind":8,"code":0,"observedAt":"2026-11-24T11:57:00Z"}""")]
    [InlineData("""{"ok":true,"stale":true,"temp":20,"wind":8,"code":0,"observedAt":"2026-11-24T11:57:00Z"}""")]
    [InlineData("""{"ok":true,"temp":75,"wind":8,"code":0,"observedAt":"2026-11-24T11:57:00Z"}""")]
    [InlineData("""{"ok":true,"temp":20,"wind":8,"code":0,"observedAt":"2026-11-24T11:00:00Z"}""")]
    [InlineData("""{"ok":true,"temp":20,"wind":8,"code":0,"observedAt":"2026-11-24T13:00:00Z"}""")]
    [InlineData("""{"ok":true,"temp":"20","wind":8,"code":0,"observedAt":"2026-11-24T11:57:00Z"}""")]
    [InlineData("""{"ok":true,"temp":20,"wind":8,"code":0}""")]
    [InlineData("""{"broken":""")]
    public void RejectsInvalidOrStalePayload(string json) =>
        Assert.Null(HostWeatherPayload.Parse("athens", json, Now));

    [Fact]
    public void PartialHostCoverageRequestsOnlyMissingCitiesAndPreservesOrder()
    {
        WeatherReading Reading(string id, double temp) => new(id, temp, 65, 8, 0, 0, true, Now);
        var requested = new[] { "athens", "patra", "larisa", "athens" };
        var shared = new[] { Reading("athens", 20) };
        Assert.Equal(new[] { "patra", "larisa" }, HostWeatherPayload.MissingCities(requested, shared));
        var merged = HostWeatherPayload.Merge(requested, shared,
            new[] { Reading("patra", 17), Reading("athens", 99), Reading("unselected", 1) },
            new[] { Reading("larisa", 15), Reading("patra", 12) });
        Assert.Equal(new[] { "athens", "patra", "larisa" }, merged.Select(r => r.CityId));
        Assert.Equal(new double[] { 20, 17, 15 }, merged.Select(r => r.TemperatureC));
    }
}

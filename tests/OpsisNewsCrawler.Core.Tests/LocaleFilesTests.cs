using System.Text.Json;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>Every shipped language file must be valid JSON with the same keys as English (a broken file silently turns the UI into raw keys).</summary>
public sealed class LocaleFilesTests
{
    private static readonly string[] Codes = { "ar", "de", "el", "en", "es", "fr", "hi", "it", "ja", "ko", "pt", "ru", "tr", "zh" };

    private static Dictionary<string, string> Load(string code)
    {
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"OpsisNewsCrawler.Locales.{code}.json")
                           ?? throw new InvalidOperationException($"locale {code} is not embedded");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    [Theory]
    [InlineData("ar")] [InlineData("de")] [InlineData("el")] [InlineData("en")] [InlineData("es")] [InlineData("fr")] [InlineData("hi")]
    [InlineData("it")] [InlineData("ja")] [InlineData("ko")] [InlineData("pt")] [InlineData("ru")] [InlineData("tr")] [InlineData("zh")]
    public void LanguageFileIsValidAndComplete(string code)
    {
        var en = Load("en");
        var other = Load(code);
        Assert.DoesNotContain(en.Keys, k => !other.ContainsKey(k));
        Assert.DoesNotContain(other, kv => string.IsNullOrWhiteSpace(kv.Value));
    }

    [Fact]
    public void EveryLanguageIsCovered() => Assert.Equal(14, Codes.Length);

    [Theory]
    [InlineData("ar")] [InlineData("de")] [InlineData("el")] [InlineData("en")] [InlineData("es")] [InlineData("fr")] [InlineData("hi")]
    [InlineData("it")] [InlineData("ja")] [InlineData("ko")] [InlineData("pt")] [InlineData("ru")] [InlineData("tr")] [InlineData("zh")]
    public void PlaceholdersMatchEnglish(string code)
    {
        var en = Load("en");
        var other = Load(code);
        foreach (var (key, text) in en)
        {
            var wanted = Enumerable.Range(0, 4).Where(i => text.Contains("{" + i + "}")).ToArray();
            var got = Enumerable.Range(0, 4).Where(i => other[key].Contains("{" + i + "}")).ToArray();
            Assert.True(wanted.SequenceEqual(got), $"{code}:{key} placeholders differ");
        }
    }

    // GearWin-style gate: every explicitly numbered release heading must be in each localized in-app history.
    [Fact]
    public void EveryChangelogVersionAppearsInEveryLocalizedHistory()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CHANGELOG.md"))) root = root.Parent;
        Assert.NotNull(root);
        var changelog = File.ReadAllText(Path.Combine(root!.FullName, "CHANGELOG.md"));
        var releases = System.Text.RegularExpressions.Regex.Matches(changelog,
            @"(?m)^#{2,3} (\d+\.\d+\.\d+)(?![\d.])").Select(m => m.Groups[1].Value).Distinct().ToArray();
        Assert.Contains("0.30.0", releases);
        Assert.True(releases.Length > 40);
        foreach (var lang in Codes)
        {
            var history = Load(lang)["history.text"];
            foreach (var release in releases)
                Assert.Matches(@"(?m)^" + System.Text.RegularExpressions.Regex.Escape(release) + @"(?![\d.])", history);
            Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(history, @"(?m)^0\.30\.0(?![\d.])").Count);
        }
    }

    // Keys that are legitimately the same in every language (product name and technical names).
    private static readonly HashSet<string> SameEverywhere = new() { "app.name", "dep.dotnet", "fmt.monitor", "note.hostTheme", "hazard.range" };

    [Theory]
    [InlineData("ar")] [InlineData("de")] [InlineData("es")] [InlineData("fr")] [InlineData("hi")] [InlineData("it")]
    [InlineData("ja")] [InlineData("ko")] [InlineData("pt")] [InlineData("ru")] [InlineData("tr")] [InlineData("zh")]
    public void NoEnglishTextLeftInOtherLanguages(string code)
    {
        var en = Load("en");
        var other = Load(code);
        var untranslated = en.Where(kv => kv.Value.Length > 12 && kv.Value.Any(char.IsLetter) && other[kv.Key] == kv.Value && !SameEverywhere.Contains(kv.Key)).Select(kv => kv.Key).ToList();
        Assert.True(untranslated.Count == 0, $"{code}: still English: {string.Join(", ", untranslated)}");
    }
}

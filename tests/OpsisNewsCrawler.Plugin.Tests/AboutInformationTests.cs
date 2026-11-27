using System.Text.Json;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public sealed class AboutInformationTests
{
    [Fact]
    public void GuideCoversEveryFunctionalPageAndUsesExistingLocalizedKeys()
    {
        Assert.Equal(17, AboutTexts.GuidePages.Count);
        Assert.Equal(17, AboutTexts.GuidePages.Select(p => p.Page).Distinct().Count());
        foreach (var code in new[] { "ar", "de", "el", "en", "es", "fr", "hi", "it", "ja", "ko", "pt", "ru", "tr", "zh" })
        {
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"OpsisNewsCrawler.Locales.{code}.json");
            Assert.NotNull(stream);
            var locale = JsonSerializer.Deserialize<Dictionary<string, string>>(stream!)!;
            foreach (var page in AboutTexts.GuidePages)
                foreach (var key in page.Features.Prepend(page.Page).Prepend(page.Group))
                    Assert.True(locale.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{code}: {key}");
            Assert.DoesNotContain("Copyright (c)", locale["license.text"]);
        }
    }

    [Fact]
    public void DisplayedLicenseIncludesExactCanonicalRepositoryText()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "LICENSE"))) root = root.Parent;
        Assert.NotNull(root);
        var expected = File.ReadAllText(Path.Combine(root!.FullName, "LICENSE")).Trim();
        Assert.EndsWith(expected, AboutTexts.License);
        Assert.Contains("Copyright (c) 2026 gs-applabs", AboutTexts.License);
        Assert.DoesNotContain("Copyright (c) 2026 Opsis Team", AboutTexts.License);
    }
}

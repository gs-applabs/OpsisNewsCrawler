using System.Text.Json;
using OpsisNewsCrawler.OpsisPlugin;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>What the Opsis 3.0 host reads from a plugin folder (PluginCapabilityManifest): opsis-permissions.json, at most 4096 bytes, three booleans.</summary>
public sealed class Opsis3CompatibilityTests
{
    private static string PermissionsFile =>
        Path.Combine(Path.GetDirectoryName(typeof(OpsisNewsCrawlerPlugin).Assembly.Location)!, "opsis-permissions.json");

    [Fact]
    public void PluginFolderCarriesAValidCapabilityDeclaration()
    {
        Assert.True(File.Exists(PermissionsFile), "opsis-permissions.json must ship next to the plugin DLL");
        Assert.True(new FileInfo(PermissionsFile).Length <= 4096);
        using var doc = JsonDocument.Parse(File.ReadAllText(PermissionsFile));
        foreach (var name in new[] { "network", "audio", "localFiles" })
            Assert.Equal(JsonValueKind.True, doc.RootElement.GetProperty(name).ValueKind);   // all three are really used
        Assert.Equal(3, doc.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void PluginAbiIdentityMatchesWhatTheHostChecks()
    {
        // Opsis 2.8 and 3.0 skip a plugin DLL whose AssemblyVersion is not exactly 1.0.0.0.
        Assert.Equal(new Version(1, 0, 0, 0), typeof(OpsisNewsCrawlerPlugin).Assembly.GetName().Version);
    }
}

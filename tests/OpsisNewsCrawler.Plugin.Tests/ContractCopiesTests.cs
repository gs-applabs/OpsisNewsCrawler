using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>
/// Contract 1.0.0.0 (additive) and the rule "every copy of the contract DLL has the same SHA-256": the canonical copy (reference\), the copy in Opsis\libs (when that repository is next to
/// this one or OPSIS_LIBS points to it), and the copy that ships with the plugin / the program (the build copies the canonical one over its output).
/// </summary>
[Collection("plugin-feeds")]
public class ContractCopiesTests
{
    private static string RefDir => Path.Combine(AppContext.BaseDirectory, "reference");
    private static string Canonical => Path.Combine(RefDir, "OpsisNewsCrawler.Plugin.Abstractions.dll");
    private static string Sha(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

    [Fact]
    public void TheCanonicalCopyMatchesItsRecordedSha256()
    {
        var recorded = File.ReadAllText(Path.Combine(RefDir, "contract.sha256")).Trim().Split(' ', '\t')[0].ToLowerInvariant();
        Assert.Equal(recorded, Sha(Canonical));
    }

    [Fact]
    public void TheCanonicalCopyIsTheCurrentContract()
    {
        // fresh build (project reference) versus canonical copy: same version and same public surface, so the canonical copy was refreshed after the last change of the source
        var ctx = new AssemblyLoadContext("canonical", isCollectible: true);
        try
        {
            var canonical = ctx.LoadFromAssemblyPath(Canonical);
            var built = typeof(PluginFeeds).Assembly;
            Assert.Equal(built.GetName().Version, canonical.GetName().Version);
            Assert.Equal(new Version(1, 0, 0, 0), canonical.GetName().Version);
            static List<string> Sig(Assembly x) => x.GetExportedTypes().SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => t.FullName + "|" + m.ToString()!.Replace(", Version=1.0.0.0", ""))).Concat(x.GetExportedTypes().Select(t => t.FullName!)).OrderBy(z => z, StringComparer.Ordinal).ToList();
            Assert.Equal(Sig(built), Sig(canonical));
        }
        finally { ctx.Unload(); }
    }

    [Fact]
    public void TheCopyNextToThePluginIsTheCanonicalOne()
    {
        // the plugin project's output (and the test output) carry the canonical DLL: the build copies it over its own
        var pluginDll = typeof(OpsisNewsCrawler.OpsisPlugin.OpsisNewsCrawlerPlugin).Assembly.Location;
        var shipped = Path.Combine(Path.GetDirectoryName(pluginDll)!, "OpsisNewsCrawler.Plugin.Abstractions.dll");
        Assert.True(File.Exists(shipped));
        Assert.Equal(Sha(Canonical), Sha(shipped));
    }

    [Fact]
    public void TheCopyInOpsisLibsIsTheSameFileWhenOpsisIsNextToThisRepository()
    {
        var candidates = new List<string>();
        var env = Environment.GetEnvironmentVariable("OPSIS_LIBS");
        if (!string.IsNullOrEmpty(env)) candidates.Add(env);
        // walk up to the repository root and look at siblings
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "OpsisNewsCrawler.sln")) && d.Parent != null)
            {
                foreach (var n in new[] { "Opsis", "MotionDeskStudio" }) candidates.Add(Path.Combine(d.Parent.FullName, n, "libs"));
                break;
            }
        var found = candidates.Select(c => Path.Combine(c, "OpsisNewsCrawler.Plugin.Abstractions.dll")).FirstOrDefault(File.Exists);
        if (found is null) return;   // not available here (CI runs tools\check-contract-sha.ps1 with the paths it has)
        Assert.Equal(Sha(Canonical), Sha(found));
    }

    private sealed class Updatable : IPluginUpdateSource, IPluginHostSettingsAware
    {
        public PluginHostSettings? Got;
        public PluginUpdateSource GetUpdateSource() => new("gs-applabs/x", "x-Plugin.zip");
        public void SetHostSettings(PluginHostSettings settings) => Got = settings;
    }

    [Fact]
    public void TheNewOptionalCapabilitiesExist()
    {
        var u = new Updatable();
        u.SetHostSettings(new PluginHostSettings("el", true, "Balanced", 125, true));
        Assert.Equal("el", u.Got!.Language);
        Assert.True(u.Got.HostManagesUpdates);
        Assert.Equal("x-Plugin.zip", u.GetUpdateSource().AssetName);
        Assert.IsAssignableFrom<IPluginUpdateSource>(new OpsisNewsCrawler.OpsisPlugin.OpsisNewsCrawlerPlugin());
    }

    [Fact]
    public void OnlyAdditionsTheOldTypesKeepTheirShape()
    {
        // the 0.8.0.0 types are untouched: the constructor of PluginNavigationItem and the interface members of the plugin are the ones the old plugins were built against
        Assert.NotNull(typeof(PluginNavigationItem).GetConstructor(new[] { typeof(string), typeof(string), typeof(string), typeof(Func<System.Windows.Forms.Control>), typeof(int) }));
        Assert.True(typeof(IOpsisNewsCrawlerPlugin).GetMembers().Length >= 15);   // Id, Name, Version, 5 lifecycle calls, SetMode/Position/Theme/Language, About, Tray, Navigation, event
    }
}

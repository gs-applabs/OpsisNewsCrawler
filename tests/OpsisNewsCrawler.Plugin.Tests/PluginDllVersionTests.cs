using System.Diagnostics;
using OpsisNewsCrawler.OpsisPlugin;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>Do not accidentally bump the shared host ABI when releasing a plugin DLL.</summary>
public sealed class PluginDllVersionTests
{
    [Fact]
    public void PluginAssemblyAndFileVersionAre1_0_0_0()
    {
        var assembly = typeof(OpsisNewsCrawlerPlugin).Assembly;
        Assert.Equal(new Version(1, 0, 0, 0), assembly.GetName().Version);
        Assert.Equal("1.0.0.0", FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion);
        Assert.Equal("0.29.0", new OpsisNewsCrawlerPlugin().Version);
    }

    [Fact]
    public void SharedContractVersionIsCoordinated1_0_0_0()
    {
        var contract = typeof(OpsisNewsCrawler.Plugin.Abstractions.IOpsisNewsCrawlerPlugin).Assembly;
        Assert.Equal(new Version(1, 0, 0, 0), contract.GetName().Version);
    }
}

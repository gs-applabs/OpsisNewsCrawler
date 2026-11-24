namespace OpsisNewsCrawler.Plugin.Abstractions;

// Source mirror for the optional Opsis 2.8 ABI. libs/ contains the authoritative
// compiled host contract DLL used by the application and plugin.
public sealed record PluginUpdateSource(string Repository, string AssetName);

public interface IPluginUpdateSource
{
    PluginUpdateSource GetUpdateSource();
}

public sealed record PluginHostSettings(
    string Language, bool Dark, string HardwareTier, int UiScalePercent, bool HostManagesUpdates);

public interface IPluginHostSettingsAware
{
    void SetHostSettings(PluginHostSettings settings);
}

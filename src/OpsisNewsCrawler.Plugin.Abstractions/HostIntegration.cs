namespace OpsisNewsCrawler.Plugin.Abstractions;

/// <summary>The settings that belong to the Opsis host, pushed to a plugin that implements <see cref="IPluginHostSettingsAware"/> (contract 1.0.0.0, additive).</summary>
/// <param name="Language">Two-letter UI language of Opsis ("el", "en", ...).</param>
/// <param name="Dark">True when the Opsis theme is dark.</param>
/// <param name="HardwareTier">"Low" | "Balanced" | "High": the performance tier the host decided on.</param>
/// <param name="UiScalePercent">Text size / UI scale in percent (100, 125, 150).</param>
/// <param name="HostManagesUpdates">True when the Opsis update button checks this plugin's releases (the plugin must not update itself).</param>
public sealed record PluginHostSettings(string Language, bool Dark, string HardwareTier, int UiScalePercent, bool HostManagesUpdates);

/// <summary>Optional: a plugin that wants the host settings (language, theme, tier, scale) instead of reading Opsis files.</summary>
public interface IPluginHostSettingsAware
{
    /// <summary>Called after the plugin started and whenever one of the values changes.</summary>
    void SetHostSettings(PluginHostSettings settings);
}

/// <summary>Where the releases of a plugin live: GitHub "owner/name" and the exact asset file name. Opsis only follows HTTPS URLs of that repository and checks the SHA-256.</summary>
/// <param name="Repository">"owner/name" of the GitHub repository.</param>
/// <param name="AssetName">Exact file name of the release asset (the plugin setup).</param>
public sealed record PluginUpdateSource(string Repository, string AssetName);

/// <summary>Optional: a plugin that tells Opsis where its releases are, so the Opsis update button can offer them (never an automatic download).</summary>
public interface IPluginUpdateSource
{
    PluginUpdateSource GetUpdateSource();
}

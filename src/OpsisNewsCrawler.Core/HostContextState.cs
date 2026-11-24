namespace OpsisNewsCrawler.Core;

/// <summary>
/// Last-value host settings bridge. Versioned; no location, credentials, I/O,
/// timers or extra members on the shared ABI 1.0.0.0 contract.
/// </summary>
public sealed record HostContextSnapshot(
    int Schema, long Revision, bool Hosted, string Language, bool? IsDark,
    string PerformanceTier, int UiScalePercent, bool? SafeMode, bool? ShellHold = null);

public sealed class HostContextState
{
    private readonly object gate = new();
    private HostContextSnapshot snapshot;

    public HostContextState(bool hosted = false)
    {
        snapshot = new HostContextSnapshot(1, 0, hosted, "en", null, "Balanced", 100, null);
    }

    public HostContextSnapshot Snapshot { get { lock (gate) return snapshot; } }

    public void AcceptTheme(bool dark) => Change(s => s with { IsDark = dark });

    /// <summary>Host-confirmed safety policy; unknown (null) never means 'safe'.</summary>
    public void AcceptSafeMode(bool value) => Change(s => s with { SafeMode = value });

    public void AcceptShellHold(bool value) => Change(s => s with { ShellHold = value });

    public void AcceptUiScale(int percent)
    {
        if (percent is < 50 or > 300) return;
        Change(s => s with { UiScalePercent = percent });
    }

    public void AcceptLanguage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 20 ||
            code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return;
        Change(s => s with { Language = code.ToLowerInvariant() });
    }

    public void AcceptTier(string? tier)
    {
        var name = tier?.Trim().ToLowerInvariant() switch
        {
            "low" => "Low", "high" => "High", _ => "Balanced"
        };
        Change(s => s with { PerformanceTier = name });
    }

    private void Change(Func<HostContextSnapshot, HostContextSnapshot> transform)
    {
        lock (gate)
        {
            var next = transform(snapshot);
            if ((next with { Revision = 0 }).Equals(snapshot with { Revision = 0 })) return;
            snapshot = next with { Revision = snapshot.Revision + 1 };
        }
    }
}

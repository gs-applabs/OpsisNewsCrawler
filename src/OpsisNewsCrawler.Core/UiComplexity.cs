namespace OpsisNewsCrawler.Core;

/// <summary>
/// Progressive settings disclosure. Never resets or removes advanced values when
/// a user switches to a simpler view. 0=simple, 1=advanced, 2=expert.
/// </summary>
public static class UiComplexity
{
    public static int Parse(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "simple" => 0, "advanced" => 1, _ => 2
    };

    public static string ToText(int index) => index switch
    {
        0 => "simple", 1 => "advanced", _ => "expert"
    };
}

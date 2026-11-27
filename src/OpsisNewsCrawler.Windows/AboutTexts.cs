using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>The Help / Instructions / Version history / License texts, in the language of the UI (see locale files).</summary>
public static class AboutTexts
{
    public const string Version = "0.30.0";

    public static string HelpTitle => Loc.T("tab.help") + " / " + Loc.T("tab.instructions");

    // Navigation labels come from the same locale keys as the real settings pages.
    public static IReadOnlyList<(string Group, string Page, string[] Features)> GuidePages { get; } =
        new (string, string, string[])[]
        {
            ("pk.settings", "pk.general", new[] { "desc.barsEnabled", "lbl.language", "lbl.refresh", "card.performance", "energy.label", "diag.button", "card.backup", "settings.export", "settings.import", "desc.startWithWindows", "pk.hot.key.ctrl.alt", "chk.hoverPause", "chk.pauseOther", "chk.icons", "chk.preview", "desc.hideRead", "btn.resetRead", "btn.openLog" }),
            ("tab.bars", "tab.position", new[] { "card.defaultBar", "lbl.position", "card.custom", "btn.moveMouse", "chk.followTaskbar", "chk.reserve", "desc.tbAuto", "lbl.tbLeft", "lbl.tbRight" }),
            ("tab.bars", "nav.barContent", new[] { "card.mainBar", "card.secondBar", "pk.split.in.the.middle", "note.secondBarBlocked", "lbl.speed", "speed.card" }),
            ("tab.bars", "pk.desktop.bar", new[] { "pk.show.a.bar.on", "lbl.miniShape", "lbl.width", "lbl.height", "chk.miniOnTop", "chk.miniWhenHidden", "btn.centerDesktop" }),
            ("tab.bars", "card.monitors", new[] { "note.monitors", "note.noOtherMonitor" }),
            ("tab.bars", "tab.look", new[] { "lbl.theme", "lbl.barColor", "lbl.barBackdrop", "lbl.taskbarBarColor", "lbl.taskbarBackdrop", "note.taskbarMaterial", "lbl.transparency", "chk.themeAccent", "chk.edgeFade", "lbl.windowOpacity" }),
            ("tab.bars", "nav.widgetStudio", new[] { "guide.widgets" }),
            ("nav.news", "pk.news.flow", new[] { "lbl.categories", "lbl.sites", "lbl.addSite", "lbl.presets", "pk.test", "pk.test.all", "pk.import.opml", "pk.export.opml", "pk.maximum.story.age.hours", "pk.newest.stories.first", "pk.only.news.about.my", "pk.mute.words.phrases.separate" }),
            ("nav.news", "tab.readLater", new[] { "note.readLater", "btn.open", "btn.removeFeed" }),
            ("nav.news", "tab.breaking", new[] { "lbl.keywords", "lbl.maxAge", "lbl.duration", "desc.breakingSound", "breaking.sensitivity", "breaking.forget", "card.live", "btn.testBreaking", "pk.severe.weather.in.my", "pk.automatic.warning.from.the" }),
            ("nav.news", "nav.intelligence", new[] { "guide.intelligence", "nav.openIntelligence" }),
            ("nav.data", "pk.markets.flow", new[] { "card.athens", "card.shares", "card.currency", "card.crypto", "card.history", "pk.price.alerts", "pk.price.source.status", "btn.openPortfolio", "btn.openAnalysis", "chk.yahooHistory", "analysis.tab.chart", "analysis.bollinger", "analysis.tab.backtest", "analysis.tab.mood", "analysis.tab.fear", "analysis.tab.ai" }),
            ("nav.data", "pk.weather.flow", new[] { "geo.card", "geo.search", "geo.add", "world.note", "pk.details.shown", "pk.click.a.city.on", "pk.the.map.openstreetmap.needs" }),
            ("nav.data", "hazard.flow", new[] { "hazard.region", "hazard.kinds", "hazard.src", "hazard.minmag", "hazard.refresh", "hazard.icon" }),
            ("nav.data", "history.flow", new[] { "history.card", "history.preview", "history.options", "history.maxLines", "history.order", "history.country", "history.addToBar" }),
            ("pk.information", "tab.updates", new[] { "chk.autoUpdate", "note.updates", "btn.checkNow", "btn.installUpdate", "btn.openPage" }),
            ("pk.information", "tab.dependencies", new[] { "dep.dotnet", "dep.webview2", "btn.installDep" })
        };

    public static string NavigationGuide => string.Join("\n\n", GuidePages.Select(p =>
        Loc.T(p.Group) + " → " + Loc.T(p.Page) + "\n" +
        string.Join("\n", p.Features.Select(k => "• " + Loc.T(k)))));

    public static string Guide => HelpTitle + " — " + Version + "\n\n" + NavigationGuide + "\n\n" + Instructions + "\n\n" + Help;

    public static string Help => Loc.T("help.text");
    public static string Instructions => Loc.T("instructions.text");
    public static string History => Loc.T("history.text");
    // The repository LICENSE is the single source of the legal text, not fourteen copies.
    private static readonly Lazy<string> LegalText = new(() =>
    {
        using var stream = typeof(AboutTexts).Assembly.GetManifestResourceStream("OpsisNewsCrawler.LICENSE")
            ?? throw new InvalidOperationException("Embedded LICENSE missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    });

    public static string License => Loc.T("license.text") + "\n\n" + LegalText.Value;
}

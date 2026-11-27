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
            ("pk.settings", "pk.general", new[] { "desc.barsEnabled", "lbl.language", "lbl.refresh", "card.performance", "card.backup", "desc.startWithWindows", "pk.hot.key.ctrl.alt", "btn.openLog" }),
            ("tab.bars", "tab.position", new[] { "card.defaultBar", "lbl.position", "card.custom", "btn.moveMouse", "desc.tbAuto", "lbl.tbLeft", "lbl.tbRight" }),
            ("tab.bars", "nav.barContent", new[] { "card.mainBar", "card.secondBar", "note.split", "lbl.speed", "speed.card" }),
            ("tab.bars", "pk.desktop.bar", new[] { "pk.show.a.bar.on", "lbl.miniShape", "lbl.width", "lbl.height", "btn.centerDesktop" }),
            ("tab.bars", "card.monitors", new[] { "note.monitors", "note.noOtherMonitor" }),
            ("tab.bars", "tab.look", new[] { "lbl.theme", "lbl.barColor", "lbl.barBackdrop", "lbl.taskbarBarColor", "lbl.taskbarBackdrop", "note.taskbarMaterial", "lbl.windowOpacity" }),
            ("tab.bars", "nav.widgetStudio", new[] { "card.mainBar", "nav.widgetStudio" }),
            ("nav.news", "pk.news.flow", new[] { "lbl.categories", "lbl.sites", "lbl.addSite", "lbl.presets", "pk.test", "pk.test.all", "pk.import.opml", "pk.export.opml" }),
            ("nav.news", "tab.readLater", new[] { "note.readLater", "btn.open", "btn.removeFeed" }),
            ("nav.news", "tab.breaking", new[] { "lbl.keywords", "lbl.maxAge", "lbl.duration", "desc.breakingSound", "card.live", "btn.testBreaking" }),
            ("nav.news", "nav.intelligence", new[] { "nav.openIntelligence" }),
            ("nav.data", "pk.markets.flow", new[] { "card.athens", "card.shares", "card.currency", "card.crypto", "card.history", "pk.price.alerts", "pk.price.source.status", "btn.openPortfolio", "btn.openAnalysis" }),
            ("nav.data", "pk.weather.flow", new[] { "geo.card", "geo.search", "geo.add", "pk.details.shown", "pk.click.a.city.on" }),
            ("nav.data", "hazard.flow", new[] { "hazard.region", "hazard.kinds", "hazard.src", "hazard.minmag", "hazard.refresh", "hazard.icon" }),
            ("nav.data", "history.flow", new[] { "history.card", "history.preview", "history.options", "history.addToBar" }),
            ("pk.information", "tab.updates", new[] { "btn.checkNow", "btn.installUpdate", "btn.openPage" }),
            ("pk.information", "tab.dependencies", new[] { "btn.installDep" })
        };

    public static string NavigationGuide => string.Join("\n\n", GuidePages.Select(p =>
        Loc.T(p.Group) + " → " + Loc.T(p.Page) + "\n" +
        string.Join("\n", p.Features.Select(k => "• " + Loc.T(k)))));

    public static string Guide => HelpTitle + " — " + Version + "\n\n" + NavigationGuide + "\n\n" + Instructions + "\n\n" + Help;

    public static string Help => Loc.T("help.text");
    public static string Instructions => Loc.T("instructions.text");
    public static string History => Loc.T("history.text");
    public static string License => Loc.T("license.text");
}

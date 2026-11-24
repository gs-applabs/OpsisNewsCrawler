using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 extracted Appearance settings pages. Shares the existing instance fields and theme infrastructure.</summary>
public sealed partial class SettingsPanel
{
    private Control BuildLook()
    {
        var page = NewPage(out var flow);
        themeMode = new FlatComboBox { Width = 300 };
        themeMode.SetItems(new[] { L("theme.system"), L("theme.light"), L("theme.dark") }.Concat(Theme.NamedThemes).Concat(host.HostedByOpsis ? new[] { L("theme.host") } : Array.Empty<string>()).ToArray(), 0);
        themeRow = Row(L("lbl.theme"), themeMode, 240);
        flow.Controls.Add(themeRow);
        themeAccent = new FlatToggle { RowWidth = 600, Text = L("chk.themeAccent") };
        flow.Controls.Add(themeAccent);

        opacity = new FlatSlider { Width = 300 };
        opacity.SetRange(30, 100);
        opacityLabel = new Label { AutoSize = false, Width = 60, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
        var op = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        op.Controls.Add(opacity);
        op.Controls.Add(opacityLabel);
        flow.Controls.Add(Row(L("lbl.transparency"), op, 240));
        opacity.ValueChanged += (_, _) => opacityLabel.Text = $"{opacity.Value} %";

        barColor = new FlatComboBox { Width = 470 };
        barColor.SetItems(new[] { L("color.theme"), L("color.lightblue"), L("color.accent"), L("color.custom") }, 0);
        barSwatch = new FlatSwatch();
        var barColorRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        barColorRow.Controls.Add(barColor);
        barColorRow.Controls.Add(barSwatch);
        flow.Controls.Add(Row(L("lbl.barColor"), barColorRow, 240));

        taskbarColor = new FlatComboBox { Width = 470 };
        taskbarColor.SetItems(new[] { L("color.taskbar"), L("color.lightblue"), L("color.accent"), L("color.custom") }, 0);
        taskbarSwatch = new FlatSwatch();
        var taskbarColorRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        taskbarColorRow.Controls.Add(taskbarColor);
        taskbarColorRow.Controls.Add(taskbarSwatch);
        flow.Controls.Add(Row(L("lbl.taskbarBarColor"), taskbarColorRow, 240));
        var materials = new[] { L("backdrop.acrylic"), L("backdrop.blur"), L("backdrop.gradient") };
        barBackdrop = new FlatComboBox { Width = 470 };
        barBackdrop.SetItems(materials, 0);
        flow.Controls.Add(Row(L("lbl.barBackdrop"), barBackdrop, 240));
        taskbarBackdrop = new FlatComboBox { Width = 470 };
        taskbarBackdrop.SetItems(materials, 1);
        flow.Controls.Add(Row(L("lbl.taskbarBackdrop"), taskbarBackdrop, 240));
        flow.Controls.Add(Note(L("note.backdrop"), 640));
        barColor.SelectedIndexChanged += (_, _) => barSwatch.Enabled = barColor.SelectedIndex == 3;
        taskbarColor.SelectedIndexChanged += (_, _) => { taskbarSwatch.Enabled = taskbarColor.SelectedIndex == 3; taskbarBackdrop.Enabled = taskbarColor.SelectedIndex != 0; };

        windowOpacity = new FlatSlider { Width = 300 };
        windowOpacity.SetRange(60, 100);
        windowOpacityLabel = new Label { AutoSize = false, Width = 60, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
        var wop = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        wop.Controls.Add(windowOpacity);
        wop.Controls.Add(windowOpacityLabel);
        windowOpacityRow = Row(L("lbl.windowOpacity"), wop, 240);
        windowOpacity.ValueChanged += (_, _) =>
        {
            windowOpacityLabel.Text = $"{windowOpacity.Value} %";
            // live preview on the settings window itself
            if (!loading && FindForm() is { } form && form.Parent is null && !HostThemeLocked) form.Opacity = windowOpacity.Value / 100.0;
        };
        flow.Controls.Add(windowOpacityRow);

        edgeFade = new FlatToggle { RowWidth = 600, Text = L("chk.edgeFade") };
        flow.Controls.Add(edgeFade);
        flow.Controls.Add(Note(L("note.transparency"), 640));
        themeNote = Note("", 640);
        flow.Controls.Add(themeNote);
        flow.Controls.Add(BuildTextAndSize());
        return page;
    }

    // ---- "History flow": the text feeds that plugins register (for example "On this day") ----------------------------------------------------

    private Control BuildTextAndSize()
    {
        using (var installed = new System.Drawing.Text.InstalledFontCollection())
            fontNames = installed.Families.Select(f => f.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        var items = new[] { L("text.defaultFont") }.Concat(fontNames).ToArray();

        var card = new FlatCard(L("text.card"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        inner.Controls.Add(Note(L("text.note"), 700));

        textColorMode = new FlatComboBox { Width = 380 };
        textColorMode.SetItems(new[] { L("text.color.auto"), L("text.color.fixed") }, 0);
        inner.Controls.Add(Row(L("text.color"), textColorMode, 260));
        inner.Controls.Add(Note(L("text.color.note"), 700));
        textShadow = new FlatToggle { RowWidth = 600, Text = L("text.shadow") };
        inner.Controls.Add(textShadow);

        void Bar(string title, out FlatComboBox font, out FlatNumber pct, out FlatToggle? auto, out FlatNumber? height)
        {
            inner.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 10, 0, 2), Font = new Font(Font, FontStyle.Bold) });
            font = new FlatComboBox { Width = 300 };
            font.SetItems(items, 0);
            inner.Controls.Add(Row(L("text.font"), font, 260));
            pct = new FlatNumber();
            pct.Configure(AppConfig.MinFontPercent, AppConfig.MaxFontPercent, 5);
            inner.Controls.Add(Row(L("text.size"), pct, 260));
            auto = null; height = null;
        }

        Bar(L("card.mainBar"), out mainFont, out mainFontPct, out _, out _);
        mainHeightAuto = new FlatToggle { RowWidth = 600, Text = L("text.heightAuto") };
        mainHeight = new FlatNumber(); mainHeight.Configure(24, 200, 2);
        mainHeightAuto.CheckedChanged += (_, _) => mainHeight.Enabled = !mainHeightAuto.Checked;
        inner.Controls.Add(mainHeightAuto);
        inner.Controls.Add(Row(L("text.height"), mainHeight, 260));

        Bar(L("card.secondBar"), out secondFont, out secondFontPct, out _, out _);
        inner.Controls.Add(Note(L("text.secondHeight"), 700));

        Bar(L("speed.mini"), out miniFont, out miniFontPct, out _, out _);
        inner.Controls.Add(Note(L("text.miniHeight"), 700));

        Bar(L("text.monitors"), out monitorFont, out monitorFontPct, out _, out _);
        monitorHeightAuto = new FlatToggle { RowWidth = 600, Text = L("text.heightAuto") };
        monitorHeight = new FlatNumber(); monitorHeight.Configure(24, 200, 2);
        monitorHeightAuto.CheckedChanged += (_, _) => monitorHeight.Enabled = !monitorHeightAuto.Checked;
        inner.Controls.Add(monitorHeightAuto);
        inner.Controls.Add(Row(L("text.height"), monitorHeight, 260));

        card.Controls.Add(inner);
        return card;
    }

    private int FontIndex(string family) => string.IsNullOrWhiteSpace(family) ? 0 : Math.Max(0, fontNames.FindIndex(n => string.Equals(n, family, StringComparison.OrdinalIgnoreCase)) + 1);

    private string FontAt(FlatComboBox combo) => combo.SelectedIndex > 0 && combo.SelectedIndex - 1 < fontNames.Count ? fontNames[combo.SelectedIndex - 1] : "";
}

using System.Reflection;
using System.Text;
using System.Text.Json;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// <c>--audit-settings &lt;report.txt&gt; --config &lt;file&gt;</c>: changes every check box, drop-down, number box and slider of the settings panel one at a time,
/// presses Apply, and reports (1) controls whose change reaches no setting at all and (2) controls whose new value is not shown again after the
/// settings are read back. Meant for a throw-away config; the program's start-with-Windows entry is never touched.
/// </summary>
public static class SettingsAudit
{
    // Controls that act outside the settings file (registry, files): not changed by the audit.
    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "startWithWindows" };

    // Helper controls of forms (add a price alert, pick a market, pick a preset): they edit a list or a temporary choice, not a stored setting.
    private static readonly HashSet<string> UiOnly = new(StringComparer.Ordinal) { "alertAge", "alertDirection", "alertInstrument", "alertMetric", "evidenceFilter", "livePriority", "marketCombo", "presets", "worldContinent", "worldCountry", "worldFeedContinent", "worldFeedCountry" };

    /// <summary>Runs the audit and writes the report; returns true when nothing is wrong (used as the exit code of the command and by CI).</summary>
    public static bool Run(TickerHost host, string reportPath)
    {
        var report = new StringBuilder();
        var panel = new SettingsPanel(host, showCloseButton: false);
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 760), ShowInTaskbar = false };
        panel.Dock = DockStyle.Fill;
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();

        var apply = typeof(SettingsPanel).GetMethod("ApplySettings", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("ApplySettings not found");
        var fields = typeof(SettingsPanel).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType.Name is "FlatCheckBox" or "FlatToggle" or "FlatComboBox" or "FlatNumber" or "FlatSlider" && !Skipped.Contains(f.Name))
            .OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

        int noEffect = 0, notShown = 0, ok = 0, skipped = 0, errors = 0;
        if (!panel.SearchFindsPage(Loc.T("lbl.taskbarBackdrop"), "appearance"))
        { errors++; report.AppendLine("ERROR     taskbar material not found by settings search"); }
        else report.AppendLine("OK        taskbar material found by settings search");
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var color = (FlatComboBox)typeof(SettingsPanel).GetField("taskbarColor", flags)!.GetValue(panel)!;
        var material = (FlatComboBox)typeof(SettingsPanel).GetField("taskbarBackdrop", flags)!.GetValue(panel)!;
        var originalColor = color.SelectedIndex;
        var originalMaterial = material.SelectedIndex;
        foreach (var tint in Enumerable.Range(0, color.Count))
        {
            color.SelectedIndex = tint;
            if (!material.Enabled) { errors++; report.AppendLine($"ERROR     taskbar material disabled for tint {tint}"); continue; }
            foreach (var preset in Enumerable.Range(0, material.Count))
            {
                material.SelectedIndex = preset;
                if (material.SelectedIndex != preset) { errors++; report.AppendLine($"ERROR     taskbar material selection {preset} failed for tint {tint}"); }
            }
        }
        color.SelectedIndex = originalColor;
        material.SelectedIndex = originalMaterial;
        panel.LoadFromConfig();
        panel.SelectTab(0);
        foreach (var section in new[] { "instructions", "license", "history" })
        {
            if (!panel.SelectPage(section) || panel.SelectedAboutSection != section)
            { errors++; report.AppendLine($"ERROR     about section {section} not reachable"); }
            else
            {
                panel.SelectPage("instructions");
                var button = panel.Controls.Find("about-" + section, true).Single();
                typeof(Control).GetMethod("OnClick", flags)!.Invoke(button, new object[] { EventArgs.Empty });
                if (panel.SelectedAboutSection != section) { errors++; report.AppendLine($"ERROR     about button {section}"); }
                else report.AppendLine($"OK        about section {section} reachable and button works");
            }
        }
        panel.SelectTab(0);
        // Check the actual host-to-window route, not just selected indices in the settings UI.
        var cfg = host.Config;
        var oldPosition = cfg.Position;
        var oldBar = cfg.BarBackdrop;
        var oldTaskbar = cfg.TaskbarBarBackdrop;
        var oldColor = cfg.BarColorMode;
        var oldTaskbarColor = cfg.TaskbarBarColorMode;
        var oldSecond = cfg.SecondBarEnabled;
        try
        {
            cfg.Position = TickerPosition.TaskbarIntegrated;
            cfg.BarBackdrop = "gradient"; cfg.BarColorMode = "custom";
            cfg.TaskbarBarBackdrop = "acrylic"; cfg.TaskbarBarColorMode = "taskbar";
            host.ApplyConfig();
            if (host.Window.ConfiguredAppearance != ("acrylic", "taskbar"))
            { errors++; report.AppendLine("ERROR     integrated primary bar uses wrong appearance"); }
            else report.AppendLine("OK        integrated primary bar uses taskbar appearance");
            cfg.Position = TickerPosition.Top;
            cfg.SecondBarEnabled = true;
            host.ApplyConfig();
            if (host.Window.ConfiguredAppearance != ("gradient", "custom") ||
                host.SecondWindow?.ConfiguredAppearance != ("acrylic", "taskbar"))
            { errors++; report.AppendLine("ERROR     separate primary/secondary appearance routing"); }
            else report.AppendLine("OK        independent primary and integrated secondary appearance");
        }
        finally
        {
            cfg.Position = oldPosition; cfg.BarBackdrop = oldBar; cfg.TaskbarBarBackdrop = oldTaskbar;
            cfg.BarColorMode = oldColor; cfg.TaskbarBarColorMode = oldTaskbarColor; cfg.SecondBarEnabled = oldSecond;
            host.ApplyConfig();
            panel.LoadFromConfig();
        }
        try { errors += AuditTopicButtons(panel, host, report); }
        catch (Exception ex) { errors++; report.AppendLine($"ERROR     topic button audit: {ex.GetType().Name}: {ex.Message}"); }
        try { errors += AuditPriceAlertButtons(panel, host, report); }
        catch (Exception ex) { errors++; report.AppendLine($"ERROR     price alert button audit: {ex}"); }
        foreach (var field in fields)
        {
            var control = field.GetValue(panel);
            if (UiOnly.Contains(field.Name))
            {
                skipped++;
                report.AppendLine($"UI-ONLY   {field.Name} (helper control of a form, no stored setting)");
                continue;
            }
            if (control is not Control c || !c.Enabled)
            {
                skipped++;
                report.AppendLine($"SKIP      {field.Name} (not created or disabled)");
                continue;
            }
            try
            {
                panel.LoadFromConfig();
                var before = Snapshot(host.Config);
                var (describe, change, back) = Prepare(control, field.Name);
                if (change is null) { skipped++; report.AppendLine($"SKIP      {field.Name} (no safe change)"); continue; }

                change();
                Application.DoEvents();
                apply.Invoke(panel, null);
                Application.DoEvents();
                var after = Snapshot(host.Config);
                var changed = Diff(before, after);

                // Read the settings back into the panel: the changed control must show the new value again.
                var shown = describe();
                panel.LoadFromConfig();
                Application.DoEvents();
                var reread = describe();

                if (changed.Count == 0)
                {
                    noEffect++;
                    report.AppendLine($"NO-EFFECT {field.Name}: changed to {shown}, Apply changed no setting");
                }
                else if (!string.Equals(shown, reread, StringComparison.Ordinal))
                {
                    notShown++;
                    report.AppendLine($"NOT-SHOWN {field.Name}: set {shown} but after reading the settings back it shows {reread} (changed: {string.Join(", ", changed)})");
                }
                else
                {
                    ok++;
                    report.AppendLine($"OK        {field.Name}: {string.Join(", ", changed)}");
                }

                back();
                apply.Invoke(panel, null);
                Application.DoEvents();
            }
            catch (Exception ex) when (ex is TargetInvocationException or InvalidOperationException or ArgumentException or NullReferenceException)
            {
                var inner = ex is TargetInvocationException { InnerException: { } i } ? i : ex;
                errors++;
                report.AppendLine($"ERROR     {field.Name}: {inner.GetType().Name}: {inner.Message}");
                report.AppendLine(inner.StackTrace ?? "(stack unavailable)");
            }
        }

        report.AppendLine();
        report.AppendLine("Keyboard checks of the custom controls:");
        var kbdFailed = KeyboardChecks(report);
        report.Insert(0, $"Settings audit of {fields.Count} controls: {ok} ok, {noEffect} change nothing, {notShown} not shown again, {skipped} skipped; keyboard checks failed: {kbdFailed}{Environment.NewLine}{Environment.NewLine}");
        File.WriteAllText(reportPath, report.ToString());
        form.Close();
        return noEffect == 0 && notShown == 0 && errors == 0 && kbdFailed == 0;
    }

    // Real Click event handlers, isolated configuration, no network or destructive external action.
    private static int AuditTopicButtons(SettingsPanel panel, TickerHost host, StringBuilder report)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        T Field<T>(string name) => (T)typeof(SettingsPanel).GetField(name, flags)!.GetValue(panel)!;
        var saved = host.Config.TopicWatches.Select(t => t.Clone()).ToList();
        var failed = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failed++;
            report.AppendLine($"{(ok ? "OK       " : "ERROR    ")} {name}");
        }
        void Click(string name)
        {
            var control = panel.Controls.Find(name, true).Single();
            typeof(Control).GetMethod("OnClick", flags)!.Invoke(control, new object[] { EventArgs.Empty });
        }
        try
        {
            host.Config.TopicWatches = new();
            panel.LoadFromConfig();
            if (!panel.SelectPage("intelligence")) throw new InvalidOperationException("Intelligence page missing");
            var name = Field<FlatText>("topicName");
            var terms = Field<FlatText>("topicTerms");
            Click("addTopic");
            Check("topic: rejects empty input", Field<List<TopicWatch>>("workTopics").Count == 0);
            name.Value = "CI topic"; terms.Value = "energy, energy, market";
            Click("addTopic");
            panel.FlushPendingChanges();
            var topic = host.Config.TopicWatches.Single();
            var id = topic.Id;
            Check("topic: Add button persists distinct terms and stable identity", topic.Terms.Count == 2 && id.Length >= 4);
            panel.LoadFromConfig();
            Check("topic: ID survives reload", Field<List<TopicWatch>>("workTopics").Single().Id == id);
            // Save an unrelated setting via its actual change event.
            var opacity = Field<FlatSlider>("opacity");
            var old = opacity.Value;
            opacity.Value = old == 100 ? 95 : 100;
            panel.FlushPendingChanges();
            Check("topic: unrelated settings save preserves ID", host.Config.TopicWatches.Single().Id == id);
            opacity.Value = old;
            panel.FlushPendingChanges();
            Field<FlatList>("topicList").SelectedIndex = 0;
            Click("removeTopic");
            panel.FlushPendingChanges();
            Check("topic: Remove button persists deletion", host.Config.TopicWatches.Count == 0);
        }
        finally
        {
            host.Config.TopicWatches = saved;
            host.SaveConfig();
            panel.LoadFromConfig();
            panel.SelectTab(0);
        }
        return failed;
    }

    private static int AuditPriceAlertButtons(SettingsPanel panel, TickerHost host, StringBuilder report)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        T Field<T>(string name) => (T)typeof(SettingsPanel).GetField(name, flags)!.GetValue(panel)!;
        var savedAlerts = host.Config.PriceAlerts.Select(a => a.Clone()).ToList();
        var savedWatch = host.Config.Watchlist.Select(a => a.Clone()).ToList();
        int failed = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failed++;
            report.AppendLine($"{(ok ? "OK       " : "ERROR    ")} {name}");
        }
        void Click(string name) =>
            typeof(Control).GetMethod("OnClick", flags)!.Invoke(panel.Controls.Find(name, true).Single(), new object[] { EventArgs.Empty });
        try
        {
            host.Config.PriceAlerts = new();
            if (!host.Config.Watchlist.Any(w => w.Enabled))
                host.Config.Watchlist = QuoteInstrument.Defaults().Take(1).Select(w => { var c = w.Clone(); c.Enabled = true; return c; }).ToList();
            panel.LoadFromConfig();
            if (!panel.SelectPage("markets")) throw new InvalidOperationException("Markets page missing");
            var instrument = Field<FlatComboBox>("alertInstrument");
            instrument.SelectedIndex = 0;
            Field<FlatComboBox>("alertMetric").SelectedIndex = 0;
            Field<FlatComboBox>("alertDirection").SelectedIndex = 0;
            Field<FlatText>("alertThreshold").Value = "-1";
            Click("addPriceAlert");
            Check("price alert: rejects invalid threshold", Field<List<PriceAlert>>("workAlerts").Count == 0);
            Field<FlatText>("alertThreshold").Value = "123.45";
            Field<FlatText>("alertMargin").Value = "0.5";
            Field<FlatNumber>("alertAge").Value = 15;
            Click("addPriceAlert");
            panel.FlushPendingChanges();
            var a = host.Config.PriceAlerts.Single();
            Check("price alert: persists threshold, margin and age", a.Threshold == 123.45m && a.RearmMargin == 0.5m && a.MaxAgeMinutes == 15);
            panel.LoadFromConfig();
            Field<FlatList>("alertList").SelectedIndex = 0;
            Click("removePriceAlert");
            panel.FlushPendingChanges();
            Check("price alert: deletion persists", host.Config.PriceAlerts.Count == 0);
        }
        finally
        {
            host.Config.PriceAlerts = savedAlerts;
            host.Config.Watchlist = savedWatch;
            host.SaveConfig();
            panel.LoadFromConfig();
            panel.SelectTab(0);
        }
        return failed;
    }

    private static (Func<string> Describe, Action? Change, Action Back) Prepare(object control, string name)
    {
        switch (control)
        {
            case FlatCheckBox check:
            {
                var original = check.Checked;
                return (() => check.Checked.ToString(), () => check.Checked = !original, () => check.Checked = original);
            }
            case FlatComboBox combo:
            {
                var original = combo.SelectedIndex;
                var count = (int?)combo.GetType().GetProperty("Count")?.GetValue(combo) ?? 0;
                if (count < 2) return (() => combo.SelectedIndex.ToString(), null, () => { });
                var next = (original + 1) % count;
                return (() => combo.SelectedIndex.ToString(), () => combo.SelectedIndex = next, () => combo.SelectedIndex = original);
            }
            case FlatNumber number:
            {
                var original = number.Value;
                // the speed boxes accept 0 (= like the main bar) or 10 and more: a step of one would be corrected by the settings code
                var next = name.StartsWith("speed", StringComparison.Ordinal) ? (original == 30 ? 40 : 30) : original < Max(number) ? original + 1 : original - 1;
                return (() => number.Value.ToString(), () => number.Value = next, () => number.Value = original);
            }
            case FlatSlider slider:
            {
                var original = slider.Value;
                var next = original < Max(slider) ? original + 1 : original - 1;
                return (() => slider.Value.ToString(), () => slider.Value = next, () => slider.Value = original);
            }
        }
        return (() => "", null, () => { });
    }

    private static void Key(Control c, Keys key, bool alt = false)
    {
        var args = new KeyEventArgs(key | (alt ? Keys.Alt : Keys.None));
        c.GetType().GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(c, new object[] { args });
    }

    private static bool Selectable(Control c) =>
        (bool)typeof(Control).GetMethod("GetStyle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(c, new object[] { ControlStyles.Selectable })!;

    private static int KeyboardChecks(StringBuilder report)
    {
        var failed = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failed++;
            report.AppendLine($"{(ok ? "OK  " : "FAIL")}      {name}");
        }

        using var combo = new FlatComboBox();
        combo.SetItems(new[] { "a", "b", "c" }, 0);
        Key(combo, Keys.Down);
        Check("combo: Down selects the next item", combo.SelectedIndex == 1);
        Key(combo, Keys.End);
        Check("combo: End selects the last item", combo.SelectedIndex == 2);
        Key(combo, Keys.Up);
        Key(combo, Keys.Up);
        Key(combo, Keys.Up);
        Check("combo: Up stops at the first item", combo.SelectedIndex == 0);
        Check("combo: reachable with Tab", Selectable(combo));

        using var slider = new FlatSlider();
        slider.SetRange(0, 100);
        slider.Value = 50;
        Key(slider, Keys.Right);
        Check("slider: Right adds 1", slider.Value == 51);
        Key(slider, Keys.PageUp);
        Check("slider: PageUp adds 10 %", slider.Value == 61);
        Key(slider, Keys.Home);
        Check("slider: Home goes to the minimum", slider.Value == 0);
        Key(slider, Keys.Left);
        Check("slider: Left stops at the minimum", slider.Value == 0);
        Check("slider: reachable with Tab", Selectable(slider));

        using var button = new FlatButton();
        Check("button: reachable with Tab", Selectable(button) && button.TabStop);
        using var check = new FlatCheckBox();
        Check("check box: reachable with Tab", Selectable(check) && check.TabStop);
        return failed;
    }
    private static int Max(object control) =>
        control.GetType().GetField("maximum", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(control) is int m ? m : int.MaxValue;

    private static Dictionary<string, string> Snapshot(AppConfig config)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(config));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
    }

    private static List<string> Diff(Dictionary<string, string> a, Dictionary<string, string> b) =>
        b.Where(kv => !a.TryGetValue(kv.Key, out var old) || old != kv.Value).Select(kv => kv.Key).ToList();
}

/// <summary><c>--snapshot-geo &lt;png&gt; &lt;query&gt; --config &lt;file&gt;</c>: types the query into the place search of the weather page, searches (real network), ticks the first two places, adds them and saves a picture.</summary>
public static class GeoSnapshot
{
    public static void Run(TickerHost host, string png, string query)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 1500), ShowInTaskbar = false };
        var panel = new SettingsPanel(host, showCloseButton: false) { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();
        if (!panel.SelectPage("weather")) throw new InvalidOperationException("Weather page missing");
        Application.DoEvents();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var t = typeof(SettingsPanel);
        var q = (FlatText)t.GetField("geoQuery", flags)!.GetValue(panel)!;
        q.Value = query;
        Wait((Task)t.GetMethod("RunGeoSearchAsync", flags)!.Invoke(panel, null)!);
        var list = (FlatList)t.GetField("geoList", flags)!.GetValue(panel)!;
        list.SetChecked(0, true);
        if (((System.Collections.IList)t.GetField("geoHits", flags)!.GetValue(panel)!).Count > 1) list.SetChecked(1, true);
        Wait((Task)t.GetMethod("AddGeoPlacesAsync", flags)!.Invoke(panel, null)!);
        for (var i = 0; i < 10; i++) { Application.DoEvents(); Thread.Sleep(40); }
        using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        bmp.Save(png);
        form.Close();
    }

    private static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(25);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(30); }
    }
}

/// <summary><c>--snapshot-tall &lt;png&gt; &lt;tab&gt; --config &lt;file&gt;</c>: one settings page on a tall window, so that cards far down the page are in the picture.</summary>
public static class TallSnapshot
{
    public static void Run(TickerHost host, string png, int tab, int height)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, height), ShowInTaskbar = false };
        var panel = new SettingsPanel(host, showCloseButton: false) { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();
        panel.SelectTab(tab);
        for (var i = 0; i < 12; i++) { Application.DoEvents(); Thread.Sleep(40); }
        using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        bmp.Save(png);
        form.Close();
    }
}

/// <summary><c>--audit-layout &lt;report&gt; --config &lt;file&gt;</c>: for every page of the settings, reports the cards wider than the common card width and controls that stick out of their card.</summary>
public static class LayoutAudit
{
    /// <summary>Writes the report; returns true when no card is wider than the common width and nothing sticks out of a card.</summary>
    public static bool Run(TickerHost host, string report)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 2600), ShowInTaskbar = false };
        var panel = new SettingsPanel(host, showCloseButton: false) { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();
        var sb = new System.Text.StringBuilder();
        for (var tab = 0; tab < panel.TabCount; tab++)
        {
            panel.SelectTab(tab);
            for (var i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(30); }
            foreach (var card in All(panel).OfType<FlatCard>().Where(c => ShownNow(c)))
            {
                // The normal width is 785 (the 780 minimum plus its border); anything wider has content that does not fit.
                if (card.Width > FlatCard.CardWidth + 10)
                {
                    var wide = All(card).Where(c => c.Right > 0).OrderByDescending(c => c.PointToScreen(new Point(c.Width, 0)).X).FirstOrDefault();
                    var heading = All(card.Parent!).OfType<Label>().FirstOrDefault(l => l.Parent == card)?.Text ?? "";
                    var rows = All(card).OfType<FlowLayoutPanel>().Where(f => f.Width > FlatCard.CardWidth - 30).Select(f => f.Controls.OfType<Control>().Select(c => c is Label lb ? lb.Text : c.GetType().Name).FirstOrDefault()).Where(s => s != null).Take(3);
                    sb.AppendLine($"tab {tab}: card '{heading}' is {card.Width}px wide (normal {FlatCard.CardWidth + 5}); wide rows: {string.Join(" | ", rows)}");
                }
                foreach (Control inner in Descendants(card))
                {
                    var right = inner.PointToScreen(new Point(inner.Width, 0)).X - card.PointToScreen(Point.Empty).X;
                    if (right > card.Width + 1 && inner.Visible) sb.AppendLine($"tab {tab}: {inner.GetType().Name} '{(inner is Label l ? l.Text : inner.Name)}' ends {right - card.Width}px outside its card");
                }
            }
        }
        File.WriteAllText(report, sb.Length == 0 ? "no card is wider than the common width; nothing sticks out\n" : sb.ToString());
        form.Close();
        return sb.Length == 0;
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var d in All(c)) yield return d;
        }
    }

    private static IEnumerable<Control> Descendants(Control root) => All(root);

    private static bool ShownNow(Control c)
    {
        for (Control? x = c; x is not null; x = x.Parent) if (!x.Visible) return false;
        return true;
    }
}

/// <summary>
/// <c>--audit-scroll &lt;report.txt&gt; --config &lt;file&gt;</c>: the "auto scroll up" check. Every page of the settings that scrolls is scrolled half way down, then the things that
/// change a live page (reading the settings again, applying, a status text, the tier text, the update state, the focus of a control at the bottom) are done one by one;
/// the page must still be where it was. Exit code 0 = no page moved.
/// </summary>
public static class ScrollAudit
{
    private static readonly string[] Actions = { "LoadFromConfig", "ApplySettings", "UpdatePerformanceInfo", "ShowUpdateState", "ShowStocks", "RebuildLiveList", "UpdateBarsUi", "UpdatePositionGroups" };

    public static bool Run(TickerHost host, string reportPath)
    {
        var report = new System.Text.StringBuilder();
        var panel = new SettingsPanel(host, showCloseButton: false);
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 760), ShowInTaskbar = false };
        panel.Dock = DockStyle.Fill;
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();
        int checks = 0, moved = 0, pages = 0, errors = 0;
        for (var tab = 0; tab < panel.TabCount; tab++)
        {
            panel.SelectTab(tab);
            for (var i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(30); }
            var page = All(panel).OfType<ScrollableControl>().FirstOrDefault(c => c.AutoScroll && Shown(c) && c.DisplayRectangle.Height > c.ClientSize.Height + 200);
            if (page is null) { report.AppendLine($"tab {tab}: no page that scrolls"); continue; }
            pages++;
            void Check(string what, Action act)
            {
                var target = Math.Max(1, (page.DisplayRectangle.Height - page.ClientSize.Height) / 2);
                page.AutoScrollPosition = new Point(0, target);
                if (page is StableScrollPanel stable) stable.ScrollTo(0, target);
                Application.DoEvents();
                var before = -page.AutoScrollPosition.Y;
                try { act(); } catch (Exception ex) when (ex is System.Reflection.TargetInvocationException or InvalidOperationException or ArgumentException or NullReferenceException)
                {
                    errors++;
                    report.AppendLine($"ERROR tab {tab}: {what}: {ex.GetBaseException().Message}");
                }
                for (var i = 0; i < 4; i++) { Application.DoEvents(); Thread.Sleep(20); }
                var after = -page.AutoScrollPosition.Y;
                checks++;
                if (Math.Abs(after - before) > 2) { moved++; report.AppendLine($"MOVED tab {tab} ({page.GetType().Name}): {what}: {before} -> {after}"); }
            }
            foreach (var name in Actions)
            {
                var method = typeof(SettingsPanel).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, Type.EmptyTypes);
                if (method is not null) Check(name, () => method.Invoke(panel, null));
            }
            var status = typeof(SettingsPanel).GetField("status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(panel) as Label;
            if (status is not null) Check("status text", () => status.Text = "a status text that changes " + Environment.TickCount);
            var tail = All(page).Where(c => c.CanSelect && c.TabStop && c.Visible).LastOrDefault();
            if (tail is not null) Check("focus on the last control", () => tail.Select());
            var label = All(page).OfType<Label>().FirstOrDefault(l => l.AutoSize && l.Visible);
            if (label is not null) Check("text of an auto-sized label", () => label.Text += " more");
        }
        report.Insert(0, $"Scroll audit: {pages} pages that scroll, {checks} checks, {moved} moved, {errors} errors\n");
        File.WriteAllText(reportPath, report.ToString());
        form.Close();
        return pages > 0 && checks > 0 && moved == 0 && errors == 0;
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var d in All(c)) yield return d;
        }
    }

    private static bool Shown(Control c)
    {
        for (Control? x = c; x is not null; x = x.Parent) if (!x.Visible) return false;
        return true;
    }
}

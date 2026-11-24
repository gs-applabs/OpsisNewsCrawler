using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>P1 Widget Studio for the five existing Opsis widgets. Uses shared
/// FlatCard / FlatToggle / FlatComboBox tokens; no duplicate host controls.</summary>
public sealed partial class SettingsPanel
{
    private readonly Dictionary<string, FlatCheckBox> widgetAccentOptions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FlatNumber> widgetFontScale =
        new(StringComparer.OrdinalIgnoreCase);
    private FlatComboBox? newsWidgetCategory;
    private List<string> newsWidgetCategoryIds = new();

    private Control BuildWidgetStudio()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(Loc.IsGreek
            ? "Ο Opsis διαχειρίζεται τη θέση, τις οθόνες και τη διάρκεια ζωής των widgets. Εδώ αλλάζετε μόνο τον τρόπο εμφάνισης των δεδομένων του Crawler."
            : "Opsis owns widget placement, monitors and lifetime. Only the Crawler's data presentation is configured here.", 690));
        // Presets update the five existing widget settings; Opsis still owns
        // monitor placement, widget windows and theme tokens.
        var presetRow = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };
        var presetChoice = new FlatComboBox { Width = 270 };
        presetChoice.SetItems(WidgetStudioPresets.Keys.Select(key =>
            WidgetStudioPresets.Title(key, Loc.IsGreek)), 0);
        var applyPreset = new FlatButton
        {
            Text = Loc.IsGreek ? "Εφαρμογή προφίλ widgets" : "Apply widget profile"
        };
        applyPreset.Click += (_, _) =>
        {
            var index = presetChoice.SelectedIndex;
            if (index < 0 || index >= WidgetStudioPresets.Keys.Count) return;
            ApplyWidgetPreset(WidgetStudioPresets.Keys[index]);
            MarkDirty();
        };
        presetRow.Controls.Add(presetChoice);
        presetRow.Controls.Add(applyPreset);
        flow.Controls.Add(Row(Loc.IsGreek ? "Έτοιμα προφίλ" : "Ready-made profiles", presetRow, 230));
        widgetEditors.Clear();
        widgetAccentOptions.Clear();
        widgetFontScale.Clear();
        newsWidgetCategory = null;
        newsWidgetCategoryIds.Clear();
        foreach (var kind in WidgetStudio.Kinds)
        {
            var card = new FlatCard(WidgetStudio.Title(kind, Loc.IsGreek))
            {
                Tag = "p1-widget-" + kind
            };
            var options = new FlowLayoutPanel
            {
                AutoSize = true, FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            if (kind == "news")
            {
                newsWidgetCategory = new FlatComboBox { Width = 260 };
                var categories = host.Config.Categories
                    .Where(c => WidgetStudio.CleanCategoryId(c.Id).Length > 0)
                    .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First()).ToList();
                newsWidgetCategoryIds = new List<string> { "" };
                newsWidgetCategoryIds.AddRange(categories.Select(c => c.Id));
                newsWidgetCategory.SetItems(
                    (new[] { Loc.IsGreek ? "Ενεργή κατηγορία μπάρας" : "Active ticker category" })
                        .Concat(categories.Select(c => Loc.IsGreek ? c.NameEl : c.NameEn)),
                    0);
                options.Controls.Add(Row(
                    Loc.IsGreek ? "Κατηγορία πηγών ειδήσεων" : "News source category",
                    newsWidgetCategory, 230));
                options.Controls.Add(Note(
                    Loc.IsGreek
                        ? "Χρησιμοποιεί τις υπάρχουσες κατηγορίες RSS, όπως Ελλάδα και Διεθνή. Δεν εντοπίζει αυτόματα την τοποθεσία σου."
                        : "Uses your existing RSS categories, such as Greece and International. No location or IP detection.",
                    600));
            }
            var layout = new FlatComboBox { Width = 260 };
            var offersGallery = WidgetStudio.CanUseGallery(kind);
            var offersMarketCards = WidgetStudio.CanUseMarketCards(kind);
            layout.SetItems(Loc.IsGreek
                ? (offersGallery ? new[] { "Τίτλοι", "Με πληροφορίες", "Κάρτες / Newsroom Gallery" }
                    : offersMarketCards ? new[] { "Τίτλοι", "Με πληροφορίες", "Οικονομικές κάρτες" }
                    : new[] { "Τίτλοι", "Με πληροφορίες" })
                : (offersGallery ? new[] { "Headlines", "With context", "Newsroom Gallery cards" }
                    : offersMarketCards ? new[] { "Headlines", "With context", "Market cards" }
                    : new[] { "Headlines", "With context" }), 0);
            var count = new FlatNumber();
            count.Configure(3, 30, 1);
            var showSource = new FlatToggle
            {
                RowWidth = 600,
                Text = Loc.IsGreek ? "Εμφάνιση πηγής" : "Show source"
            };
            var showTime = new FlatToggle
            {
                RowWidth = 600,
                Text = kind == "history"
                    ? (Loc.IsGreek ? "Ώρα δημοσίευσης (δεν αφορά ιστορικά γεγονότα)" : "Publication time (not used for historical events)")
                    : (Loc.IsGreek ? "Εμφάνιση ώρας δημοσίευσης" : "Show publication time"),
                Enabled = kind != "history"
            };
            options.Controls.Add(Row(Loc.IsGreek ? "Διάταξη" : "Layout", layout, 230));
            options.Controls.Add(Row(Loc.IsGreek ? "Μέγιστες εγγραφές" : "Maximum rows", count, 230));
            var fontScale = new FlatNumber();
            fontScale.Configure(80, 160, 5);
            options.Controls.Add(Row(Loc.IsGreek ? "Μέγεθος γραμμάτων (%)" : "Text size (%)",
                fontScale, 230));
            widgetFontScale[kind] = fontScale;
            options.Controls.Add(showSource);
            options.Controls.Add(showTime);
            var hostAccent = new FlatToggle
            {
                RowWidth = 600, Enabled = host.HostedByOpsis,
                Text = Loc.IsGreek ? "Χρώμα έμφασης από το Opsis" : "Use Opsis accent for this widget"
            };
            options.Controls.Add(hostAccent);
            widgetAccentOptions[kind] = hostAccent;
            card.Controls.Add(options);
            flow.Controls.Add(card);
            widgetEditors[kind] = (layout, count, showSource, showTime);
        }
        return page;
    }

    private void ApplyWidgetPreset(string name)
    {
        var profiles = WidgetStudioPresets.Create(name);
        foreach (var (kind, controls) in widgetEditors)
        {
            var p = profiles[kind];
            controls.Layout.SelectSilently(p.Layout switch { "context" => 1, "gallery" or "market-cards" => 2, _ => 0 });
            controls.Count.SetValueSilently(p.MaxItems);
            if (widgetFontScale.TryGetValue(kind, out var fontScale))
                fontScale.SetValueSilently(p.FontScalePercent);
            controls.Source.Checked = p.ShowSource;
            controls.Time.Checked = p.ShowTime;
            if (kind == "news" && newsWidgetCategory is not null)
                newsWidgetCategory.SelectSilently(0); // apply preset: active category
            if (widgetAccentOptions.TryGetValue(kind, out var accent))
                accent.Checked = p.AccentMode == "host";
        }
    }

    private void LoadWidgetStudio()
    {
        foreach (var (kind, controls) in widgetEditors)
        {
            var profile = WidgetStudio.Resolve(host.Config.WidgetProfiles, kind);
            controls.Layout.SelectSilently(profile.Layout switch
            {
                "context" => 1,
                "gallery" when WidgetStudio.CanUseGallery(kind) => 2,
                "market-cards" when WidgetStudio.CanUseMarketCards(kind) => 2,
                _ => 0
            });
            controls.Count.SetValueSilently(profile.MaxItems);
            if (widgetFontScale.TryGetValue(kind, out var fontScale))
                fontScale.SetValueSilently(profile.FontScalePercent);
            controls.Source.Checked = profile.ShowSource;
            controls.Time.Checked = profile.ShowTime;
            if (kind == "news" && newsWidgetCategory is not null)
            {
                var index = newsWidgetCategoryIds.FindIndex(id => string.Equals(
                    id, profile.NewsCategoryId, StringComparison.OrdinalIgnoreCase));
                newsWidgetCategory.SelectSilently(index < 0 ? 0 : index);
            }
            if (widgetAccentOptions.TryGetValue(kind, out var accent))
                accent.Checked = profile.AccentMode == "host";
        }
    }

    private Dictionary<string, WidgetStudioProfile> ReadWidgetStudio()
    {
        var selected = new Dictionary<string, WidgetStudioProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var (kind, controls) in widgetEditors)
        {
            selected[kind] = WidgetStudio.Clean(new WidgetStudioProfile
            {
                Layout = controls.Layout.SelectedIndex switch
                {
                    1 => "context",
                    2 when WidgetStudio.CanUseGallery(kind) => "gallery",
                    2 when WidgetStudio.CanUseMarketCards(kind) => "market-cards",
                    _ => "headlines"
                },
                MaxItems = controls.Count.Value,
                FontScalePercent = widgetFontScale.TryGetValue(kind, out var fontScale)
                    ? fontScale.Value : 100,
                ShowSource = controls.Source.Checked,
                ShowTime = controls.Time.Checked,
                AccentMode = widgetAccentOptions.TryGetValue(kind, out var accent) &&
                    accent.Checked ? "host" : "theme",
                NewsCategoryId = kind == "news" && newsWidgetCategory is not null &&
                    newsWidgetCategory.SelectedIndex >= 0 &&
                    newsWidgetCategory.SelectedIndex < newsWidgetCategoryIds.Count
                    ? newsWidgetCategoryIds[newsWidgetCategory.SelectedIndex] : ""
            });
        }
        return selected;
    }
}

using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>P1 Widget Studio for the five existing Opsis widgets. Uses shared
/// FlatCard / FlatToggle / FlatComboBox tokens; no duplicate host controls.</summary>
public sealed partial class SettingsPanel
{
    private Control BuildWidgetStudio()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(Loc.IsGreek
            ? "Ο Opsis διαχειρίζεται τη θέση, τις οθόνες και τη διάρκεια ζωής των widgets. Εδώ αλλάζετε μόνο τον τρόπο εμφάνισης των δεδομένων του Crawler."
            : "Opsis owns widget placement, monitors and lifetime. Only the Crawler's data presentation is configured here.", 690));
        widgetEditors.Clear();
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
            var layout = new FlatComboBox { Width = 260 };
            var offersGallery = WidgetStudio.CanUseGallery(kind);
            layout.SetItems(Loc.IsGreek
                ? (offersGallery ? new[] { "Τίτλοι", "Με πληροφορίες", "Κάρτες / Newsroom Gallery" } : new[] { "Τίτλοι", "Με πληροφορίες" })
                : (offersGallery ? new[] { "Headlines", "With context", "Newsroom Gallery cards" } : new[] { "Headlines", "With context" }), 0);
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
            options.Controls.Add(showSource);
            options.Controls.Add(showTime);
            card.Controls.Add(options);
            flow.Controls.Add(card);
            widgetEditors[kind] = (layout, count, showSource, showTime);
        }
        return page;
    }

    private void LoadWidgetStudio()
    {
        foreach (var (kind, controls) in widgetEditors)
        {
            var profile = WidgetStudio.Resolve(host.Config.WidgetProfiles, kind);
            controls.Layout.SelectSilently(profile.Layout switch { "context" => 1, "gallery" when WidgetStudio.CanUseGallery(kind) => 2, _ => 0 });
            controls.Count.SetValueSilently(profile.MaxItems);
            controls.Source.Checked = profile.ShowSource;
            controls.Time.Checked = profile.ShowTime;
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
                    _ => "headlines"
                },
                MaxItems = controls.Count.Value,
                ShowSource = controls.Source.Checked,
                ShowTime = controls.Time.Checked
            });
        }
        return selected;
    }
}

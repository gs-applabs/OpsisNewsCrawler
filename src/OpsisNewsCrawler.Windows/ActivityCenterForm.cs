using OpsisNewsCrawler.Core;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace OpsisNewsCrawler.Windows;

/// <summary>One Opsis-themed activity centre: alerts and an interactive, date-grouped timeline.</summary>
internal sealed class ActivityCenterForm : Form
{
    private readonly ActivityJournal journal;
    private readonly FlatComboBox category = new() { Width = 165 };
    private readonly FlatComboBox period = new() { Width = 185 };
    private readonly FlatButton export = new();
    private readonly FlatButton close = new();
    private readonly Label count = new() { Dock = DockStyle.Bottom, Height = 29, AutoEllipsis = true };
    private readonly ListView items = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, ShowGroups = true, HideSelection = false,
        BorderStyle = BorderStyle.None,
        AccessibleName = "Opsis activity timeline"
    };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 15000 };
    private static readonly string[] Kinds = { "", "Breaking", "News", "Market", "Weather", "Hazard", "Calendar" };

    public ActivityCenterForm(ActivityJournal journal)
    {
        this.journal = journal;
        Text = Loc.T("p2.activity.title");
        Icon = AppIcon.Get();
        Font = new Font("Segoe UI",9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1040,620);
        MinimumSize = new Size(700,400);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 55, AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = new Padding(12,9,12,4)
        };
        category.SetItems(new[]
        {
            Loc.T("p2.activity.all"),Loc.T("p2.activity.breaking"),Loc.T("p2.activity.news"),
            Loc.T("p2.activity.market"),Loc.T("p2.activity.weather"),
            Loc.T("p2.activity.hazard"),Loc.T("p2.activity.calendar")
        },0);
        period.SetItems(new[]
        {
            Loc.T("p2.activity.today"),Loc.T("p2.activity.week"),
            Loc.T("p2.activity.month"),Loc.T("p2.activity.any")
        },3);
        export.Text = Loc.T("p2.activity.export");
        close.Text = Loc.T("p2.activity.close");
        bar.Controls.Add(category);
        bar.Controls.Add(period);
        bar.Controls.Add(export);
        bar.Controls.Add(close);
        category.SelectedIndexChanged += (_,_) => Populate();
        period.SelectedIndexChanged += (_,_) => Populate();
        export.Click += (_,_) => Export();
        close.Click += (_,_) => Close();
        items.Columns.Add(Loc.T("p2.activity.time"),155);
        items.Columns.Add(Loc.T("p2.activity.kind"),110);
        items.Columns.Add(Loc.T("p2.activity.headline"),570);
        items.Columns.Add(Loc.T("p2.activity.origin"),160);
        items.DoubleClick += (_,_) => OpenSelected();
        items.KeyDown += (_,e) => { if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled=true; } };
        Controls.Add(items);
        Controls.Add(count);
        Controls.Add(bar);
        Theme.Changed += Restyle;
        refresh.Tick += (_,_) => Populate();
        Shown += (_,_) => { Restyle(); Populate(); refresh.Start(); };
        VisibleChanged += (_,_) => { if (!Visible) refresh.Stop(); };
    }

    private void Restyle()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(new Action(Restyle)); return; }
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        items.BackColor = Theme.Surface;
        items.ForeColor = Theme.Text;
        count.BackColor = Theme.Background;
        count.ForeColor = Theme.TextMuted;
        Theme.Apply(this);
    }

    private IReadOnlyList<ActivityItem> VisibleEvents()
    {
        var kind = category.SelectedIndex > 0 && category.SelectedIndex < Kinds.Length
            ? Kinds[category.SelectedIndex] : null;
        var days = period.SelectedIndex switch { 1 => 7, 2 => 30, _ => 0 };
        var result = journal.Snapshot(kind, days);
        return period.SelectedIndex == 0 ? result.Where(x => x.At.LocalDateTime.Date == DateTime.Today).ToArray() : result;
    }

    private void Populate()
    {
        if (IsDisposed || !IsHandleCreated) return;
        var displayed = VisibleEvents();
        var selected = items.SelectedItems.Count > 0 && items.SelectedItems[0].Tag is ActivityItem prior ? prior.Key : null;
        items.BeginUpdate();
        try
        {
            items.Items.Clear();
            items.Groups.Clear();
            foreach (var group in displayed.GroupBy(x => x.At.LocalDateTime.Date))
            {
                var heading = group.Key.ToString("dddd dd MMMM yyyy",CultureInfo.CurrentCulture);
                var listGroup = new ListViewGroup(heading);
                items.Groups.Add(listGroup);
                foreach (var entry in group)
                {
                    var row = new ListViewItem(entry.At.ToLocalTime().ToString("dd/MM HH:mm"),listGroup)
                    { Tag = entry };
                    row.SubItems.Add(entry.Kind);
                    row.SubItems.Add(entry.Title);
                    row.SubItems.Add(entry.Source);
                    items.Items.Add(row);
                    if (entry.Key == selected) row.Selected = true;
                }
            }
            count.Text = Loc.T("p2.activity.count", displayed.Count, journal.Snapshot().Count);
        }
        finally { items.EndUpdate(); }
    }

    private void OpenSelected()
    {
        if (items.SelectedItems.Count == 0 || items.SelectedItems[0].Tag is not ActivityItem item) return;
        if (!NetGuard.IsPublicHttpUrl(item.Url)) return;
        try { Process.Start(new ProcessStartInfo(item.Url!) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private void Export()
    {
        using var chooser = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "opsis-activity-" + DateTime.Now.ToString("yyyyMMdd",CultureInfo.InvariantCulture) + ".json",
            AddExtension = true
        };
        if (chooser.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // Only the displayed filter is exported; no background sharing or upload.
            File.WriteAllText(chooser.FileName, JsonSerializer.Serialize(VisibleEvents(),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this,ex.Message,Loc.T("p2.activity.title"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { refresh.Stop(); refresh.Dispose(); Theme.Changed -= Restyle; }
        base.Dispose(disposing);
    }
}

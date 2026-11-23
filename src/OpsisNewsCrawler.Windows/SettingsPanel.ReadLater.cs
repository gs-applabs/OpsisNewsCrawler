using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 extracted ReadLater settings pages. Shares the existing instance fields and theme infrastructure.</summary>
public sealed partial class SettingsPanel
{
    private Control BuildReadLater()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("note.readLater"), 700));
        var card = new FlatCard(L("tab.readLater"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        laterList = new FlatList { Width = 740, Height = 320 };
        laterList.SetShowChecks(true);
        inner.Controls.Add(laterList);
        var open = new FlatButton { Text = L("btn.open") };
        var remove = new FlatButton { Text = L("btn.removeFeed") };
        var all = new FlatButton { Text = L("btn.selectAll") };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(open);
        buttons.Controls.Add(remove);
        buttons.Controls.Add(all);
        inner.Controls.Add(buttons);
        laterStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(780, 0), Margin = new Padding(0, 6, 0, 0) };
        inner.Controls.Add(laterStatus);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        open.Click += (_, _) =>
        {
            foreach (var a in CheckedLater().Take(20)) OpenInBrowser(a.Url);
        };
        remove.Click += (_, _) => host.ReadLater.Remove(CheckedLater().Select(a => a.Url));
        all.Click += (_, _) =>
        {
            for (var i = 0; i < laterShown.Count; i++) laterList.SetChecked(i, true);
        };
        RefreshReadLater();
        return page;
    }

    private List<SavedArticle> CheckedLater() =>
        laterShown.Where((_, i) => laterList.GetChecked(i)).ToList();

    private void OnReadLaterChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(new Action(RefreshReadLater));
        else RefreshReadLater();
    }

    private void RefreshReadLater()
    {
        if (laterList is null || laterList.IsDisposed) return;
        laterShown = host.ReadLater.Items.ToList();
        laterList.ClearRows();
        foreach (var a in laterShown)
            laterList.AddRow($"{a.Source}  —  {a.Title}", false);
        laterStatus.Text = laterShown.Count == 0 ? L("msg.readLaterEmpty") : Loc.T("fmt.readLaterCount", laterShown.Count);
    }

    /// <summary>Opens a saved article in the browser (http/https only).</summary>
}

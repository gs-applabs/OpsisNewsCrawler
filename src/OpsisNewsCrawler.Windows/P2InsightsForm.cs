using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using System.ComponentModel;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>Opsis-themed P2 research center; reads cached news and snapshots without polling sources.</summary>
public sealed class P2InsightsForm : Form
{
    private readonly TickerHost host;
    private readonly FlatComboBox views = new() { Width=300 };
    private readonly FlatButton export = new();
    private readonly FlatButton refresh = new();
    private readonly Label summary = new() { Dock=DockStyle.Bottom, Height=34, AutoEllipsis=true };
    private readonly ListView lines = new()
    {
        Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true,
        GridLines=false, HideSelection=false, MultiSelect=false
    };
    private readonly List<(string Name, string Detail, string? Url)> visible = new();

    public P2InsightsForm(TickerHost host)
    {
        this.host=host;
        Text=Loc.IsGreek ? "Opsis News Crawler 0.28.0 — Κέντρο πληροφοριών" : "Opsis News Crawler 0.28.0 — Intelligence";
        StartPosition=FormStartPosition.CenterParent;
        Size=new Size(1060,680);
        MinimumSize=new Size(720,460);
        Font=new Font("Segoe UI",9.5f);
        AutoScaleMode=AutoScaleMode.Dpi;
        lines.Columns.Add(Loc.IsGreek ? "Γεγονός / είδηση" : "Event / story",690);
        lines.Columns.Add(Loc.IsGreek ? "Πηγή / στοιχεία" : "Source / details",290);
        lines.AccessibleRole=AccessibleRole.List;
        lines.AccessibleName=Loc.IsGreek ? "Ροή πληροφοριών" : "Intelligence feed";
        var bar=new FlowLayoutPanel { Dock=DockStyle.Top, Height=54,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(10,8,8,0)};
        views.SetItems(Loc.IsGreek ?
            new[]{"Χρονολόγιο","Ομάδες ειδήσεων","Παρακολούθηση θεμάτων","Συναλλαγές χαρτοφυλακίου"}
            :new[]{"Timeline","Story clusters","Topic watchlist","Portfolio transactions"},0);
        views.SelectedIndexChanged+=(_,_)=>Reload();
        refresh.Text=Loc.IsGreek?"Ανανέωση προβολής":"Refresh view";
        refresh.Click+=(_,_)=>Reload();
        export.Text=Loc.IsGreek?"Εξαγωγή HTML ημέρας":"Export daily HTML";
        export.Click+=(_,_)=>ExportDigest();
        bar.Controls.Add(views);bar.Controls.Add(refresh);bar.Controls.Add(export);
        Controls.Add(lines);Controls.Add(summary);Controls.Add(bar);
        lines.DoubleClick+=(_,_)=>OpenSelected();
        lines.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){OpenSelected();e.Handled=true;}};
        Theme.Changed+=ApplyLook;
        Shown+=(_,_)=>{ApplyLook();Reload();};
    }

    private void ApplyLook()
    {
        if(IsDisposed)return;
        BackColor=Theme.Background;ForeColor=Theme.Text;
        summary.BackColor=Theme.Background;summary.ForeColor=Theme.TextMuted;
        lines.BackColor=Theme.Surface;lines.ForeColor=Theme.Text;
    }

    private void Reload()
    {
        if(IsDisposed || !IsHandleCreated)return;
        visible.Clear();
        var news=host.RecentNews;
        switch(views.SelectedIndex)
        {
            case 1:
                foreach(var g in StoryExplorer.Cluster(news).Take(150))
                    visible.Add((g.Headline,
                        $"{g.Reports.Count} reports · {g.PublisherGroups} publisher groups · {g.LastUpdate:dd/MM HH:mm}",g.Key));
                break;
            case 2:
                foreach(var m in TopicMatcher.Find(news,host.Config.TopicWatches))
                    visible.Add(($"{m.Topic.Name} · {m.Item.Title}",$"{m.Score}/100 · {m.Item.Source}",m.Item.Url));
                break;
            case 3:
                foreach(var group in host.Config.PortfolioTransactions.GroupBy(x=>x.InstrumentKey).Take(100))
                {
                    try
                    {
                        var result=PortfolioLedger.Calculate(group);
                        visible.Add((group.Key,
                            $"{result.Units:0.####} units · cost {result.CostBasis:0.00} · realized {result.RealizedPnl:0.00} · dividends {result.Dividends:0.00}",null));
                    }
                    catch(InvalidOperationException)
                    {
                        visible.Add((group.Key,Loc.IsGreek?"Μη συνεπές ιστορικό συναλλαγών":"Inconsistent transaction ledger",null));
                    }
                }
                break;
            default:
                var events=SuiteEventHub.Snapshot().Select(e=>
                    new TimelineEvent(e.Key,e.Title,e.Kind.ToString(),e.At,e.HistoricalYear,e.Origin,e.Url));
                foreach(var line in SuiteTimeline.Build(news,events,200))
                    visible.Add((line.Title,
                        $"{(line.HistoricalYear is { } y ? y.ToString()+" · " : "")}{line.Category} · {line.Source} · {line.At:dd/MM HH:mm}",line.Url));
                break;
        }
        lines.BeginUpdate();
        try
        {
            lines.Items.Clear();
            foreach(var row in visible)
                lines.Items.Add(new ListViewItem(new[]{row.Name,row.Detail}));
            summary.Text=Loc.IsGreek?$"Εγγραφές: {visible.Count} · Εκτός σύνδεσης · Τα δεδομένα πηγών μπορεί να καθυστερούν."
                :$"Items: {visible.Count} · Offline view · Source data may be delayed.";
        }
        finally {lines.EndUpdate();}
    }

    private void OpenSelected()
    {
        var index=lines.SelectedIndices.Count>0?lines.SelectedIndices[0]:-1;
        if(index<0 || index>=visible.Count)return;
        var url=visible[index].Url;
        if(!NetGuard.IsPublicHttpUrl(url))return;
        try{Process.Start(new ProcessStartInfo(url!) {UseShellExecute=true});}
        catch(Exception ex) when(ex is InvalidOperationException or Win32Exception){}
    }

    private void ExportDigest()
    {
        using var save=new SaveFileDialog
        {
            Title=Loc.IsGreek?"Εξαγωγή ημερήσιας σύνοψης":"Export daily digest",
            Filter="HTML (*.html)|*.html",DefaultExt="html",
            FileName="OpsisNewsCrawler-"+DateTime.Today.ToString("yyyyMMdd")+".html"
        };
        if(save.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            NewsDigest.SaveHtml(save.FileName,host.RecentNews,
                DateOnly.FromDateTime(DateTime.Today),Loc.IsGreek?"Ημερήσια σύνοψη":"Daily digest");
            summary.Text=save.FileName;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing)Theme.Changed-=ApplyLook;
        base.Dispose(disposing);
    }
}

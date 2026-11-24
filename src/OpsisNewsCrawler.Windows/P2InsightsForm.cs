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
    private readonly FlatButton exportWeekly = new();
    private readonly FlatButton importCalendar = new();
    private readonly FlatButton exportCalendar = new();
    private readonly FlatButton evidence = new();
    private readonly FlatButton newTransaction = new();
    private readonly FlatButton mediaPlaylist = new();
    private readonly FlatButton refresh = new();
    private readonly FlatComboBox clusterFilter = new() { Width=250 };
    private readonly List<StoryCluster> displayedClusters = new();
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
            new[]{"Χρονολόγιο","Ομάδες ειδήσεων","Παρακολούθηση θεμάτων","Συναλλαγές χαρτοφυλακίου","Οικονομικό ημερολόγιο"}
            :new[]{"Timeline","Story clusters","Topic watchlist","Portfolio transactions","Economic calendar"},0);
        views.SelectedIndexChanged+=(_,_)=>Reload();
        clusterFilter.SetItems(Loc.IsGreek ? new[]{ "Όλες οι ομάδες", "Πολλαπλές ομάδες εκδοτών", "Μία ή άγνωστη ομάδα" }
            : new[]{ "All clusters", "Multiple publisher groups", "One or unknown group" },0);
        clusterFilter.Visible=false;
        clusterFilter.SelectedIndexChanged+=(_,_)=>Reload();
        refresh.Text=Loc.IsGreek?"Ανανέωση προβολής":"Refresh view";
        refresh.Click+=(_,_)=>Reload();
        export.Text=Loc.IsGreek?"Εξαγωγή HTML ημέρας":"Export daily HTML";
        export.Click+=(_,_)=>ExportDigest();
        exportWeekly.Text=Loc.IsGreek?"Εβδομάδα HTML":"Weekly HTML";
        exportWeekly.Click+=(_,_)=>ExportWeeklyDigest();
        importCalendar.Text=Loc.IsGreek?"Εισαγωγή ημερολογίου":"Import calendar";
        importCalendar.Click+=(_,_)=>ImportCalendar();
        exportCalendar.Text="ICS";
        exportCalendar.Click+=(_,_)=>ExportCalendar();
        bar.WrapContents=true;
        bar.Height=105;
        bar.Controls.Add(views);bar.Controls.Add(clusterFilter);bar.Controls.Add(refresh);bar.Controls.Add(export);
        evidence.Text=Loc.IsGreek?"Σύγκριση πηγών":"Compare sources";
        evidence.Click+=(_,_)=>CompareSelectedStory();
        newTransaction.Text=Loc.IsGreek?"Νέα συναλλαγή":"Add trade";
        newTransaction.Click+=(_,_)=>AddTransaction();
        mediaPlaylist.Text=Loc.IsGreek?"Media Suite · M3U":"Media Suite · M3U";
        mediaPlaylist.Click+=(_,_)=>ExportMediaPlaylist();
        bar.Controls.Add(exportWeekly);bar.Controls.Add(importCalendar);bar.Controls.Add(exportCalendar);
        bar.Controls.Add(evidence);bar.Controls.Add(newTransaction);bar.Controls.Add(mediaPlaylist);
        bar.Height=155;
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
        displayedClusters.Clear();
        clusterFilter.Visible=views.SelectedIndex==1;
        evidence.Enabled=views.SelectedIndex==1;
        var news=host.RecentNews;
        switch(views.SelectedIndex)
        {
            case 1:
                var clusters=StoryExplorer.Cluster(news).AsEnumerable();
                clusters=clusterFilter.SelectedIndex switch
                {
                    1 => clusters.Where(g=>g.PublisherGroups>=2),
                    2 => clusters.Where(g=>g.PublisherGroups<2),
                    _ => clusters
                };
                displayedClusters.AddRange(clusters.Take(150));
                foreach(var g in displayedClusters)
                    visible.Add((g.Headline,
                        Loc.IsGreek
                            ? $"{g.Reports.Count} δημοσιεύματα · {g.PublisherGroups} ομάδες εκδοτών · {g.LastUpdate:dd/MM HH:mm}"
                            : $"{g.Reports.Count} reports · {g.PublisherGroups} publisher groups · {g.LastUpdate:dd/MM HH:mm}",g.Key));
                break;
            case 2:
                foreach(var m in TopicMatcher.Find(news,host.Config.TopicWatches))
                    visible.Add(($"{m.Topic.Name} · {m.Item.Title}",$"{m.Score}/100 · {m.Item.Source}",m.Item.Url));
                break;
            case 4:
                foreach(var entry in EconomicCalendar.Upcoming(host.Config.EconomicEvents,DateTimeOffset.Now,90))
                    visible.Add((entry.Title,$"{entry.StartsAt:dd/MM/yyyy HH:mm} · {entry.Country} · {entry.Source} · impact {entry.Impact}",entry.Url));
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
        if(views.SelectedIndex==1){CompareSelectedStory();return;}
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

    private void CompareSelectedStory()
    {
        var index=lines.SelectedIndices.Count>0?lines.SelectedIndices[0]:-1;
        if(index<0||index>=visible.Count||views.SelectedIndex!=1)return;
        if(index>=displayedClusters.Count)return;
        var cluster=displayedClusters[index];
        using var form=new P3EvidenceForm(cluster);
        form.ShowDialog(this);
    }

    private void AddTransaction()
    {
        using var form=new P3TransactionForm(host);
        if(form.ShowDialog(this)==DialogResult.OK)Reload();
    }

    private void ExportMediaPlaylist()
    {
        using var save=new SaveFileDialog
        {
            Title=Loc.IsGreek?"Εξαγωγή πηγών πολυμέσων για Media Suite":"Export media links for Media Suite",
            Filter="M3U playlist (*.m3u)|*.m3u",DefaultExt="m3u",
            FileName="OpsisMediaSuite-News.m3u"
        };
        if(save.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            var media=host.RecentNews.Select(MediaSuiteBridge.FromNews)
                .Where(x=>x is not null).Cast<MediaSuiteAsset>().ToList();
            // Only streams explicitly selected in settings, never unauthorized rebroadcasts.
            media.AddRange(host.Config.LiveFeeds.Where(x=>x.Enabled)
                .Select(x=>new MediaSuiteAsset(x.Url,x.Name,"User configured",null))
                .Where(MediaSuiteBridge.IsSafeRemoteAsset));
            File.WriteAllText(save.FileName,MediaSuiteBridge.ExportM3u(media));
            summary.Text=Loc.IsGreek?"Αποθηκεύτηκε λίστα συνδέσμων. Ανοίξτε την στο Media Suite."
                :"Saved source playlist. Open it from Media Suite.";
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private void ExportWeeklyDigest()
    {
        using var save=new SaveFileDialog
        {
            Title=Loc.IsGreek?"Εβδομαδιαία σύνοψη":"Weekly digest",
            Filter="HTML (*.html)|*.html",DefaultExt="html",
            FileName="OpsisWeekly-"+DateTime.Today.ToString("yyyyMMdd")+".html"
        };
        if(save.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            var html=WeeklyDigest.Html(host.RecentNews,DateOnly.FromDateTime(DateTime.Today),
                Loc.IsGreek?"Εβδομαδιαία σύνοψη":"Weekly digest");
            File.WriteAllText(save.FileName,html);
            summary.Text=save.FileName;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private void ImportCalendar()
    {
        using var open=new OpenFileDialog {Filter="JSON (*.json)|*.json",Title=Loc.IsGreek?
            "Εισαγωγή οικονομικού ημερολογίου από αξιόπιστο αρχείο":"Import economic calendar from trusted file"};
        if(open.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            var info=new FileInfo(open.FileName);
            if(info.Length>1_000_000)throw new InvalidDataException("Calendar exceeds 1 MB.");
            var imported=EconomicCalendar.ImportJson(File.ReadAllText(open.FileName));
            var existing=host.Config.EconomicEvents;
            // Duplicates are replaced by stable ID; imported data never triggers automatic downloads.
            host.Config.EconomicEvents=existing.Concat(imported).GroupBy(x=>x.Id,StringComparer.OrdinalIgnoreCase)
                .Select(g=>g.Last()).OrderBy(x=>x.StartsAt).TakeLast(1000).ToList();
            host.SaveConfig(); views.SelectedIndex=4;Reload();
            summary.Text=Loc.IsGreek?$"Εισαγωγή: {imported.Count} γεγονότα":$"Imported {imported.Count} calendar events";
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }

    private void ExportCalendar()
    {
        using var save=new SaveFileDialog {Filter="iCalendar (*.ics)|*.ics",DefaultExt="ics",
            FileName="OpsisEconomicCalendar.ics"};
        if(save.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            File.WriteAllText(save.FileName,
                EconomicCalendar.ExportIcs(EconomicCalendar.Upcoming(host.Config.EconomicEvents,
                    DateTimeOffset.Now,90)));
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

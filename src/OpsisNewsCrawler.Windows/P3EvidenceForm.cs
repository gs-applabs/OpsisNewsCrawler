using OpsisNewsCrawler.Core;
using System.ComponentModel;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>Manual review of corroboration and correction indicators; never calls a story "true".</summary>
public sealed class P3EvidenceForm : Form
{
    private readonly ListView reportList=new()
    {
        Dock=DockStyle.Fill,FullRowSelect=true,MultiSelect=true,View=View.Details,
        AccessibleName="Publisher report comparison"
    };
    private readonly IReadOnlyList<NewsItem> reports;
    private readonly TableLayoutPanel details=new() { Dock=DockStyle.Bottom,Height=150,ColumnCount=2,RowCount=1 };
    private readonly Label left=new() { Dock=DockStyle.Fill,AutoEllipsis=true,Padding=new Padding(8),UseMnemonic=false };
    private readonly Label right=new() { Dock=DockStyle.Fill,AutoEllipsis=true,Padding=new Padding(8),UseMnemonic=false };

    public P3EvidenceForm(StoryCluster cluster)
    {
        var review=FactCheckSupport.Review(cluster);
        reports=review.Reports;
        Text=Loc.IsGreek?"Σύγκριση πηγών · Opsis":"Source comparison · Opsis";
        StartPosition=FormStartPosition.CenterParent;
        Size=new Size(920,570); MinimumSize=new Size(630,380);
        Font=new Font("Segoe UI",9.5f); AutoScaleMode=AutoScaleMode.Dpi;
        var header=new Label {Dock=DockStyle.Top,Height=72,AutoEllipsis=true,
            Text=cluster.Headline+"\n"+
                 (Loc.IsGreek?$"{review.PublisherGroups} ομάδες εκδοτών · Δεν αποτελεί επαλήθευση."
                    :$"{review.PublisherGroups} publisher groups · Not fact verification.")};
        reportList.Columns.Add(Loc.IsGreek?"Πηγή":"Source",180);
        reportList.Columns.Add(Loc.IsGreek?"Τίτλος δημοσιεύματος":"Headline",480);
        reportList.Columns.Add(Loc.IsGreek?"Ώρα":"Time",120);
        reportList.Columns.Add(Loc.IsGreek?"Διόρθωση;":"Correction?",100);
        foreach(var item in reports)
            reportList.Items.Add(new ListViewItem(new[]{
                item.Source,item.Title,item.PublishedAt.ToLocalTime().ToString("dd/MM HH:mm"),
                FactCheckSupport.MentionsRetraction(item)?"Review":"—"
            }));
        reportList.DoubleClick+=(_,_)=>Open();
        reportList.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){Open();e.Handled=true;}};
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        details.Controls.Add(left,0,0);details.Controls.Add(right,1,0);
        left.Text=Loc.IsGreek?"Επιλέξτε δημοσίευμα για τη διαθέσιμη περίληψη RSS· με Ctrl+κλικ επιλέξτε δεύτερο για σύγκριση δίπλα δίπλα.":"Select a report for its RSS summary; Ctrl+click a second one to compare them side by side.";
        reportList.SelectedIndexChanged+=(_,_)=>ShowSelected();
        Controls.Add(reportList);Controls.Add(details);Controls.Add(header);
        BackColor=Theme.Background; ForeColor=Theme.Text;
        header.BackColor=Theme.Surface;header.ForeColor=Theme.Text;
        reportList.BackColor=Theme.Surface;reportList.ForeColor=Theme.Text;
        details.BackColor=Theme.Surface;details.ForeColor=Theme.Text;
        left.ForeColor=right.ForeColor=Theme.Text;
    }

    /// <summary>N18: one or two selected reports side by side, only their RSS headline and summary (no full article is copied).</summary>
    private void ShowSelected()
    {
        var chosen=reportList.SelectedIndices.Cast<int>().Where(i=>i>=0&&i<reports.Count).Take(2).ToList();
        left.Text=chosen.Count>0?Describe(reports[chosen[0]]):"";
        right.Text=chosen.Count>1?Describe(reports[chosen[1]]):"";
    }

    private static string Describe(NewsItem item)
    {
        var text=item.Summary??"";
        if(text.Length>700)text=text[..700]+"…";
        return item.Source+" · "+item.PublishedAt.ToLocalTime().ToString("dd/MM HH:mm")+"\n"+item.Title+"\n\n"+
            (string.IsNullOrWhiteSpace(text)?(Loc.IsGreek?"Δεν υπάρχει περίληψη RSS.":"No RSS summary available."):text);
    }

    private void Open()
    {
        var index=reportList.SelectedIndices.Count>0?reportList.SelectedIndices[0]:-1;
        if(index<0||index>=reports.Count)return;
        var url=reports[index].Url;
        if(!NetGuard.IsPublicHttpUrl(url))return;
        try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
        catch(Exception e) when(e is InvalidOperationException or Win32Exception){}
    }
}

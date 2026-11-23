using OpsisNewsCrawler.Core;
using System.ComponentModel;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>Manual review of corroboration and correction indicators; never calls a story "true".</summary>
public sealed class P3EvidenceForm : Form
{
    private readonly ListView reportList=new()
    {
        Dock=DockStyle.Fill,FullRowSelect=true,MultiSelect=false,View=View.Details,
        AccessibleName="Independent report comparison"
    };
    private readonly IReadOnlyList<NewsItem> reports;

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
        Controls.Add(reportList);Controls.Add(header);
        BackColor=Theme.Background; ForeColor=Theme.Text;
        header.BackColor=Theme.Surface;header.ForeColor=Theme.Text;
        reportList.BackColor=Theme.Surface;reportList.ForeColor=Theme.Text;
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

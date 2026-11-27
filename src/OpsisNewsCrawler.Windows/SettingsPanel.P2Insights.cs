using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P2 panel using the same Opsis flat components and automatic save behavior.</summary>
public sealed partial class SettingsPanel
{
    private List<TopicWatch> workTopics = new();
    private FlatList topicList = null!;
    private FlatText topicName = null!, topicTerms = null!, topicRegion = null!;
    private Label topicStatus = null!;

    private Control BuildP2Insights()
    {
        var page=NewPage(out var flow);
        var card=new FlatCard(Loc.IsGreek ? "Παρακολούθηση θεμάτων" : "Topic watchlists");
        var inner=new FlowLayoutPanel { AutoSize=true, FlowDirection=FlowDirection.TopDown, WrapContents=false };
        inner.Controls.Add(Note(Loc.IsGreek
            ? "Τοπικά φίλτρα χωρίς εξωτερική ανάλυση. Όνομα, λέξεις κλειδιά χωρισμένες με κόμμα και προαιρετική περιοχή."
            : "Private offline filters. Enter a name, comma-separated keywords and optional region.",720));
        topicList=new FlatList { Width=720, Height=160 };
        topicList.SetShowChecks(true);
        topicList.ItemChecked += (i,yes) => { if(i>=0 && i<workTopics.Count) workTopics[i].Enabled=yes; };
        inner.Controls.Add(topicList);
        topicName=new FlatText { Width=210 };
        topicTerms=new FlatText { Width=300 };
        topicRegion=new FlatText { Width=150 };
        topicName.AccessibleName = L("col.name");
        topicTerms.AccessibleName = Loc.IsGreek ? "Λέξεις-κλειδιά (με κόμμα)" : "Keywords (comma-separated)";
        topicRegion.AccessibleName = Loc.IsGreek ? "Περιοχή (προαιρετικά)" : "Region (optional)";
        inner.Controls.Add(Row(topicName.AccessibleName, topicName, 235));
        inner.Controls.Add(Row(topicTerms.AccessibleName, topicTerms, 235));
        inner.Controls.Add(Row(topicRegion.AccessibleName, topicRegion, 235));
        var add=new FlatButton { Text=Loc.IsGreek ? "Προσθήκη θέματος" : "Add topic" };
        add.Click += (_,_) =>
        {
            var name=topicName.Value.Trim();
            var terms=topicTerms.Value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
                .Where(t=>t.Length>=3).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
            if(name.Length==0 || terms.Count==0)
            { topicStatus.Text=Loc.IsGreek ? "Χρειάζεται όνομα και μία λέξη τουλάχιστον." : "Enter a name and at least one keyword."; return; }
            workTopics.Add(new TopicWatch{Name=name,Terms=terms,Region=topicRegion.Value.Trim()});
            RebuildTopics(); MarkDirty(); topicName.Value=""; topicTerms.Value=""; topicRegion.Value="";
        };
        var remove=new FlatButton { Text=Loc.IsGreek ? "Αφαίρεση" : "Remove" };
        remove.Click += (_,_) =>
        {
            if(topicList.SelectedIndex < 0 || topicList.SelectedIndex>=workTopics.Count)return;
            workTopics.RemoveAt(topicList.SelectedIndex); RebuildTopics(); MarkDirty();
        };
        var action=new FlowLayoutPanel {AutoSize=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true};
        action.Controls.Add(add); action.Controls.Add(remove); inner.Controls.Add(action);
        topicStatus=new Label {AutoSize=true,Tag="muted"};
        inner.Controls.Add(topicStatus);
        card.Controls.Add(inner); flow.Controls.Add(card);

        var insights=new FlatCard(Loc.IsGreek ? "Χρονολόγιο · Πηγές · Ημερήσια σύνοψη" : "Timeline · Sources · Daily digest");
        var insightFlow=new FlowLayoutPanel {AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        insightFlow.Controls.Add(Note(Loc.IsGreek
            ? "Συγκεντρωμένες υπάρχουσες ειδήσεις, πηγές και γεγονότα της οικογένειας Opsis. Η εξαγωγή HTML λειτουργεί χωρίς δίκτυο."
            : "Current local news, sources and Opsis suite events. HTML digest export works offline.",720));
        var open=new FlatButton {Text=L("nav.openIntelligence")};
        open.SetPrimary(true);
        open.Click += (_,_) => {using var form=new P2InsightsForm(host);form.ShowDialog(this);};
        insightFlow.Controls.Add(open); insights.Controls.Add(insightFlow); flow.Controls.Add(insights);
        return page;
    }

    private void RebuildTopics()
    {
        if(topicList is null || topicList.IsDisposed)return;
        topicList.ClearRows();
        foreach(var topic in workTopics)
            topicList.AddRow($"{topic.Name}  —  {string.Join(", ",topic.Terms)}  ·  {topic.Region}",topic.Enabled);
    }
}

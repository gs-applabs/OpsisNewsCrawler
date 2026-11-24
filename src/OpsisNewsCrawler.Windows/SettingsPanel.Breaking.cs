using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 extracted Breaking settings pages. Shares the existing instance fields and theme infrastructure.</summary>
public sealed partial class SettingsPanel
{
    private FlatList breakingEvidenceList = null!;
    private Label breakingEvidenceDetails = null!;
    private List<BreakingAssessment> breakingEvidenceRows = new();

    private void RefreshBreakingEvidence()
    {
        if (breakingEvidenceList is null || breakingEvidenceList.IsDisposed) return;
        breakingEvidenceRows = host.BreakingEvidence.Take(30).ToList();
        breakingEvidenceList.ClearRows();
        foreach (var a in breakingEvidenceRows)
        {
            var title = a.Item.Title.Length > 82 ? a.Item.Title[..82] + "…" : a.Item.Title;
            breakingEvidenceList.AddRow($"{a.Score}/100 · {a.Publishers} {(Loc.IsGreek ? "ομάδες πηγών" : "source groups")} · {title}");
        }
        breakingEvidenceDetails.Text = breakingEvidenceRows.Count == 0
            ? (Loc.IsGreek ? "Δεν υπάρχουν ακόμη αξιολογήσεις. Οι ειδήσεις αξιολογούνται με τις επόμενες ανανεώσεις."
                : "No assessments yet. Breaking evaluation runs on the next news refresh.")
            : (Loc.IsGreek ? "Επιλέξτε μια εγγραφή για αναλυτική αιτιολόγηση." : "Select an item for its scoring evidence.");
    }

    private void ShowBreakingEvidenceDetails()
    {
        var index = breakingEvidenceList.SelectedIndex;
        if (index < 0 || index >= breakingEvidenceRows.Count) return;
        var assessment = breakingEvidenceRows[index];
        breakingEvidenceDetails.Text =
            BreakingWhy.Detailed(assessment, BreakingEngine.ParseSensitivity(host.Config.BreakingSensitivity)) +
            Environment.NewLine +
            (Loc.IsGreek ? "Η βαθμολογία δεν πιστοποιεί ότι το γεγονός είναι αληθές."
                : "The score does not certify that the story is true.");
    }

    private Control BuildBreaking()
    {
        var page = NewPage(out var flow);
        breakingEnabled = new FlatToggle { RowWidth = 600, Text = L("chk.breakingEnabled") };
        flow.Controls.Add(breakingEnabled);
        breakingSound = new FlatToggle { RowWidth = 600, Text = Loc.T("pk.alert.sound.when.something") };
        flow.Controls.Add(breakingSound);
        breakingSensitivity = new FlatComboBox { Width = 300 };
        breakingSensitivity.SetItems(new[] { L("breaking.sens.strict"), L("breaking.sens.normal"), L("breaking.sens.sensitive") }, 1);
        flow.Controls.Add(Row(L("breaking.sensitivity"), breakingSensitivity, 330));
        flow.Controls.Add(Note(L("breaking.sensitivity.note"), 700));
        keywords = new FlatText { Width = 520 };
        flow.Controls.Add(Row(L("lbl.keywords"), keywords, 330));
        forgetBreaking = new FlatButton { Text = L("breaking.forget") };
        forgetBreaking.Click += (_, _) => { host.ForgetBreakingFeedback(); forgetBreaking.Text = L("breaking.forgotten"); };
        flow.Controls.Add(forgetBreaking);
        maxAge = new FlatNumber();
        maxAge.Configure(10, 1440, 10);
        flow.Controls.Add(Row(L("lbl.maxAge"), maxAge, 330));
        duration = new FlatNumber();
        duration.Configure(1, 240, 5);
        flow.Controls.Add(Row(L("lbl.duration"), duration, 330));
        flow.Controls.Add(Note(L("note.breaking"), 700));

        // Severe weather for the user's area.
        var weatherCard = new FlatCard(Loc.T("pk.severe.weather.in.my"));
        var weatherFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        weatherAlerts = new FlatToggle { RowWidth = 600, Text = Loc.T("pk.warn.me.about.severe") };
        weatherFlow.Controls.Add(weatherAlerts);
        alertCity = new FlatComboBox { Width = 360 };
        FillAlertCities(null);
        weatherFlow.Controls.Add(Row(Loc.T("pk.my.area"), alertCity, 200));
        weatherFlow.Controls.Add(Note(Loc.T("pk.every.30.minutes.the"), 640));
        var testWeather = new FlatButton { Text = Loc.T("pk.test.the.weather.warning") };
        testWeather.Click += (_, _) => host.SimulateWeatherAlert();
        weatherFlow.Controls.Add(testWeather);
        weatherCard.Controls.Add(weatherFlow);
        flow.Controls.Add(weatherCard);

        var test = new FlatButton { Text = L("btn.testBreaking") };
        test.Click += (_, _) => host.SimulateBreaking(Loc.T("pk.breaking.test.headline.to"));
        flow.Controls.Add(test);

        // P1: explain algorithm scores and independent publisher groups without
        // implying that a ranked story has been fact-checked or confirmed true.
        var evidenceCard = new FlatCard(Loc.IsGreek ? "Τεκμηρίωση έκτακτων ειδήσεων" : "Breaking evidence");
        var evidenceFlow = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        evidenceFlow.Controls.Add(Note(Loc.IsGreek
            ? "Ο δείκτης 0–100 είναι εκτίμηση προτεραιότητας, ΟΧΙ πιθανότητα αλήθειας ή ανεξάρτητη επαλήθευση."
            : "The 0–100 score ranks priority. It is NOT a truth probability or independent fact-check.", 680));
        breakingEvidenceList = new FlatList { Width = 640, Height = 150 };
        evidenceFlow.Controls.Add(breakingEvidenceList);
        var evidenceButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var refreshEvidence = new FlatButton { Text = Loc.IsGreek ? "Ενημέρωση αξιολογήσεων" : "Refresh assessments" };
        refreshEvidence.Click += (_, _) => RefreshBreakingEvidence();
        var detailsEvidence = new FlatButton { Text = Loc.IsGreek ? "Γιατί βαθμολογήθηκε έτσι;" : "Explain selected score" };
        detailsEvidence.Click += (_, _) => ShowBreakingEvidenceDetails();
        evidenceButtons.Controls.Add(refreshEvidence);
        evidenceButtons.Controls.Add(detailsEvidence);
        evidenceFlow.Controls.Add(evidenceButtons);
        breakingEvidenceDetails = Note("", 680);
        evidenceFlow.Controls.Add(breakingEvidenceDetails);
        evidenceCard.Controls.Add(evidenceFlow);
        flow.Controls.Add(evidenceCard);
        RefreshBreakingEvidence();

        var card = new FlatCard(L("card.live"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        inner.Controls.Add(Note(L("note.live"), 620));
        liveList = new FlatList { Width = 640, Height = 30 * 4 + 4 };
        liveList.SetShowChecks(true);
        liveList.ItemChecked += (index, isChecked) =>
        {
            if (index < workLive.Count) workLive[index].Enabled = isChecked;
        };
        inner.Controls.Add(liveList);
        liveList.SelectedIndexChanged += (_, _) => LoadSelectedLiveMetadata();

        // P1 live hub controls reuse the existing Opsis-styled flat inputs.
        // Metadata influences ranking of user-configured sources only.
        inner.Controls.Add(Note(Loc.IsGreek
            ? "Προτεραιότητα, γεωγραφική περιοχή και λέξεις-κλειδιά: δεν αποδεικνύουν αξιοπιστία ούτε επιβεβαιώνουν την άδεια αναμετάδοσης."
            : "Priority, region and keywords rank your own feeds; they do not verify a channel or grant rebroadcasting rights.", 650));
        livePriority = new FlatNumber();
        livePriority.Configure(-100, 100, 5);
        liveRegion = new FlatText { Width = 110 };
        liveKeywords = new FlatText { Width = 440 };
        inner.Controls.Add(Row(Loc.IsGreek ? "Προτεραιότητα (-100 έως 100)" : "Priority (-100 to 100)", livePriority, 230));
        inner.Controls.Add(Row(Loc.IsGreek ? "Περιοχή (GR, CY κ.λπ.)" : "Region (GR, CY etc.)", liveRegion, 230));
        inner.Controls.Add(Row(Loc.IsGreek ? "Λέξεις-κλειδιά (με ;)" : "Keywords (separate with ;)", liveKeywords, 230));
        livePriority.ValueChanged += (_, _) => CommitSelectedLiveMetadata();
        liveRegion.TextValueChanged += (_, _) => CommitSelectedLiveMetadata();
        liveKeywords.TextValueChanged += (_, _) => CommitSelectedLiveMetadata();

        var previewPriority = new FlatButton { Text = Loc.IsGreek ? "Προεπισκόπηση κατάταξης live" : "Preview live ranking" };
        previewPriority.Click += (_, _) => PreviewLiveRanking();
        inner.Controls.Add(previewPriority);
        var removeLive = new FlatButton { Text = L("btn.removeLive") };
        removeLive.Click += (_, _) =>
        {
            if (liveList.SelectedIndex < 0 || liveList.SelectedIndex >= workLive.Count) return;
            workLive.RemoveAt(liveList.SelectedIndex);
            RebuildLiveList();
        };
        inner.Controls.Add(removeLive);
        inner.Controls.Add(new Label { Text = L("lbl.addLive"), AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
        var add = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        addLiveBox = new FlatText { Width = 440 };
        var addBtn = new FlatButton { Text = L("btn.addLive") };
        addBtn.Click += (_, _) => AddLive();
        add.Controls.Add(addLiveBox);
        add.Controls.Add(addBtn);
        inner.Controls.Add(add);
        liveStatus = new Label { AutoSize = true, Tag = "muted" };
        inner.Controls.Add(liveStatus);
        card.Controls.Add(inner);
        flow.Controls.Add(card);
        return page;
    }

    private void RebuildLiveList()
    {
        if (liveList is null || liveList.IsDisposed) return;
        var oldIndex = liveList.SelectedIndex;
        liveList.ClearRows();
        foreach (var f in workLive)
            liveList.AddRow($"{(string.IsNullOrWhiteSpace(f.Name) ? f.Url : f.Name)}   [{f.Region}, {f.Priority:+#;-#;0}]  —  {f.Url}", f.Enabled);
        if (workLive.Count > 0) liveList.SelectedIndex = Math.Clamp(oldIndex, 0, workLive.Count - 1);
    }

    private void LoadSelectedLiveMetadata()
    {
        if (livePriority is null || liveRegion is null || liveKeywords is null) return;
        var index = liveList.SelectedIndex;
        if (index < 0 || index >= workLive.Count) return;
        liveMetadataLoading = true;
        var wasLoading = loading;
        loading = true; // selecting a row is not itself a settings modification
        try
        {
            var feed = workLive[index];
            livePriority.SetValueSilently(feed.Priority);
            liveRegion.Value = feed.Region ?? "";
            liveKeywords.Value = string.Join("; ", feed.EventKeywords ?? new());
        }
        finally
        {
            loading = wasLoading;
            liveMetadataLoading = false;
        }
    }

    private void CommitSelectedLiveMetadata()
    {
        if (liveMetadataLoading) return;
        var index = liveList.SelectedIndex;
        if (index < 0 || index >= workLive.Count) return;
        var feed = workLive[index];
        feed.Priority = livePriority.Value;
        feed.Region = liveRegion.Value;
        feed.EventKeywords = liveKeywords.Value.Split(';', StringSplitOptions.TrimEntries |
            StringSplitOptions.RemoveEmptyEntries).ToList();
        workLive[index] = LiveVideoStudio.Clean(feed);
    }

    private void PreviewLiveRanking()
    {
        CommitSelectedLiveMetadata();
        var ordered = LiveVideoPolicy.Select(workLive, host.BreakingVideoContext);
        liveStatus.Text = ordered.Count == 0
            ? (Loc.IsGreek ? "Δεν υπάρχουν έγκυρες ενεργές ροές." : "No valid enabled streams.")
            : string.Join("  ·  ", ordered.Take(3).Select((x, i) => $"{i + 1}. {x.Name}"));
        // Read-only preview: no WebView2 is created and no stream is opened.
    }

    private void AddLive()
    {
        // "Name;Address" (the address may contain ';' only after the first one)
        var split = addLiveBox.Value.Split(';', 2, StringSplitOptions.TrimEntries);
        var name = split.Length > 1 ? split[0] : "";
        var url = split.Length > 1 ? split[1] : split[0];
        if (Breaking.ToPlayableUrl(url) is null)
        {
            liveStatus.Text = L("msg.badLive");
            return;
        }
        workLive.Add(new LiveFeed(name, url));
        RebuildLiveList();
        addLiveBox.Value = "";
        liveStatus.Text = "";
    }

    /// <summary>A read-only page with one of the long texts (help, instructions, history, license).</summary>
    /// <summary>A read-only text page. With a <paramref name="host"/> it has two buttons on top: the welcome guide and the Opsis family window.</summary>
}

using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 extracted History settings pages. Shares the existing instance fields and theme infrastructure.</summary>
public sealed partial class SettingsPanel
{
    private Control BuildHistory()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("history.note"), 700));

        var card = new FlatCard(L("history.card"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        historyNone = new Label { AutoSize = true, MaximumSize = new Size(740, 0), Tag = "muted", Text = L("history.none") };
        inner.Controls.Add(historyNone);
        historyList = new FlatList { Width = 740, Height = 30 * 4 + 4 };
        historyList.SetShowChecks(true);
        historyList.ItemChecked += (index, isChecked) =>
        {
            if (refreshingHistory || index < 0 || index >= historyFeeds.Count) return;
            var key = historyFeeds[index].Key;
            workDisabledFeeds.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (!isChecked) workDisabledFeeds.Add(key);
        };
        historyList.SelectedIndexChanged += (_, _) => { if (!refreshingHistory) { historyRetries = 0; RefreshHistoryPreview(); } };
        inner.Controls.Add(historyList);
        historyUsed = new Label { AutoSize = true, MaximumSize = new Size(740, 0), Tag = "muted", Margin = new Padding(0, 6, 0, 4) };
        inner.Controls.Add(historyUsed);
        historyAddToBar = new FlatButton { Text = L("history.addToBar") };
        historyAddToBar.Click += (_, _) => AddSelectedFeedToMainBar();
        inner.Controls.Add(historyAddToBar);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        var preview = new FlatCard(L("history.preview"));
        historyPreviewCard = preview;
        var previewFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        historyStatus = new Label { AutoSize = true, MaximumSize = new Size(740, 0), Tag = "muted", Margin = new Padding(0, 0, 0, 4) };
        previewFlow.Controls.Add(historyStatus);
        historyPreview = new FlatList { Width = 740, Height = 30 * 8 + 4 };
        previewFlow.Controls.Add(historyPreview);
        preview.Controls.Add(previewFlow);
        flow.Controls.Add(preview);

        var optionsCard = new FlatCard(L("history.options"));
        var optionsFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        optionsFlow.Controls.Add(Note(L("history.options.note"), 700));
        feedMaxLines = new FlatNumber();
        feedMaxLines.Configure(5, 200, 5);
        optionsFlow.Controls.Add(Row(L("history.maxLines"), feedMaxLines, 300));
        feedOrder = new FlatComboBox { Width = 380 };
        feedOrder.SetItems(new[] { L("history.order.priority"), L("history.order.yearAsc"), L("history.order.yearDesc") }, 0);
        optionsFlow.Controls.Add(Row(L("history.order"), feedOrder, 300));
        feedCountry = new FlatComboBox { Width = 380 };
        feedCountryCodes = new List<string> { "" };
        var countryNames = new List<string> { L("history.country.all") };
        foreach (var c in WorldPlaces.Continents.SelectMany(k => k.Countries).OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)) { feedCountryCodes.Add(c.Code); countryNames.Add(c.DisplayName); }
        feedCountry.SetItems(countryNames, 0);
        optionsFlow.Controls.Add(Row(L("history.country"), feedCountry, 300));
        optionsCard.Controls.Add(optionsFlow);
        flow.Controls.Add(optionsCard);

        return page;
    }

    private void OnHistoryRetry(object? sender, EventArgs e)
    {
        historyRetry.Stop();
        if (IsDisposed || Disposing || ++historyRetries > 10) return;
        RefreshHistoryPreview();
    }

    private void OnPluginFeedsChanged()
    {
        // raised by a plugin, from any thread
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(() => { if (!IsDisposed) { historyRetries = 0; foreach (var picker in AllPickers()) picker.SetChoices(workCategories); RefreshHistoryFeeds(); } })); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
    }

    private void RefreshHistoryFeeds()
    {
        if (historyList is null) return;
        var selectedKey = historyList.SelectedIndex >= 0 && historyList.SelectedIndex < historyFeeds.Count ? historyFeeds[historyList.SelectedIndex].Key : null;
        historyFeeds = PluginFeedMapper.AllFeeds(() => PluginFeedOptions.From(host.Config, Loc.Language)).ToList();
        refreshingHistory = true;
        try
        {
            historyList.ClearRows();
            foreach (var f in historyFeeds)
                historyList.AddRow($"{Loc.Pick(f.TitleEl, f.TitleEn)}   -   {f.Key}", !workDisabledFeeds.Contains(f.Key, StringComparer.OrdinalIgnoreCase));
            if (historyFeeds.Count > 0)
                historyList.SelectedIndex = Math.Max(0, historyFeeds.FindIndex(f => string.Equals(f.Key, selectedKey, StringComparison.OrdinalIgnoreCase)));
        }
        finally { refreshingHistory = false; }
        var any = historyFeeds.Count > 0;
        historyNone.Visible = !any;
        historyList.Visible = historyUsed.Visible = historyAddToBar.Visible = historyPreviewCard.Visible = any;
        RefreshHistoryPreview();
    }

    private void RefreshHistoryPreview()
    {
        if (historyPreview is null || historyStatus is null) return;
        historyPreview.ClearRows();
        if (historyFeeds.Count == 0)
        {
            historyUsed.Text = "";
            return;
        }
        var index = Math.Max(0, historyList.SelectedIndex);
        var feed = historyFeeds[Math.Min(index, historyFeeds.Count - 1)];
        historyUsed.Text = UsedInText(feed.Key);
        IReadOnlyList<OpsisNewsCrawler.Plugin.Abstractions.PluginFeedItem> items;
        try { items = feed.GetItems(); }   // the lines are kept in memory by the plugin (the contract allows no input/output here); an error shows as empty
        catch (Exception ex) { Log.Write($"Plugin feed '{feed.Key}' preview failed: {ex.Message}"); items = Array.Empty<OpsisNewsCrawler.Plugin.Abstractions.PluginFeedItem>(); }
        if (items.Count == 0)
        {
            historyStatus.Text = L("history.loading");
            if (historyRetries < 10) historyRetry.Start();
            return;
        }
        historyStatus.Text = Loc.T("history.count", items.Count, Loc.Pick(feed.TitleEl, feed.TitleEn));
        foreach (var item in items.Take(200)) historyPreview.AddRow(item.Text);
    }

    private string UsedInText(string key)
    {
        var spec = "feed:" + key;
        var names = new List<string>();
        void Check(SlotPicker picker, string name)
        {
            if (picker is not null && picker.Get().All().Any(s => s.ToSpec().Equals(spec, StringComparison.OrdinalIgnoreCase))) names.Add(name);
        }
        Check(mainPicker, L("card.mainBar"));
        Check(secondPicker, L("card.secondBar"));
        Check(miniPicker, L("speed.mini"));
        foreach (var row in monitorRows) Check(row.Picker, row.Screen.DeviceName.TrimStart('\\', '.'));
        return names.Count == 0 ? L("history.notUsed") : Loc.T("history.usedIn", string.Join(", ", names));
    }

    /// <summary>Puts the selected feed in the right half of the main bar (the bar is split in the middle); what the left half shows stays as it is.</summary>
    private void AddSelectedFeedToMainBar()
    {
        if (historyList.SelectedIndex < 0 || historyList.SelectedIndex >= historyFeeds.Count) return;
        var feed = historyFeeds[historyList.SelectedIndex];
        workDisabledFeeds.RemoveAll(k => string.Equals(k, feed.Key, StringComparison.OrdinalIgnoreCase));
        var current = mainPicker.Get();
        var slot = new BarSlot(SlotKind.Feed, feed.Key);
        mainPicker.Set(current.A == slot ? new BarSlots(slot, null) : new BarSlots(current.A, slot));
        RefreshHistoryFeeds();
        MarkDirty();
    }

    // ---- font, text size and height of every bar ----------------------------------------------------------------------------------------------
}

using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>Market watchlists, price alerts and source-status settings.</summary>
public sealed partial class SettingsPanel
{
    private FlatText alertMargin = null!;
    private FlatNumber alertAge = null!;

    private Control BuildMarkets()
    {
        var page = NewPage(out var flow);
        var openPortfolio = new FlatButton { Text = L("btn.openPortfolio") };
        openPortfolio.SetPrimary(true);
        openPortfolio.Click += (_, _) => host.ShowPortfolio();
        flow.Controls.Add(openPortfolio);
        var openAnalysis = new FlatButton { Text = L("btn.openAnalysis") };
        openAnalysis.Click += (_, _) => host.ShowAnalysis();
        flow.Controls.Add(openAnalysis);
        var historyCard = new FlatCard(L("card.history"));
        var historyFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        yahooHistory = new FlatToggle { RowWidth = 600, Text = L("chk.yahooHistory") };
        historyFlow.Controls.Add(yahooHistory);
        historyFlow.Controls.Add(Note(L("note.yahooHistory"), 700));
        historyCard.Controls.Add(historyFlow);
        flow.Controls.Add(historyCard);

        // The Yahoo Finance catalogue: search any share, index, currency or commodity it offers, or add a ready-made group.
        var catalog = new FlatCard(L("card.yahooCatalog"));
        var catalogFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        catalogFlow.Controls.Add(Note(L("yahoo.note"), 700));
        yahooSearch = new FlatText { Width = 380 };
        yahooSearchButton = new FlatButton { Text = L("btn.yahooSearch") };
        yahooSearchButton.SetPrimary(true);
        var searchRow2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        searchRow2.Controls.Add(yahooSearch);
        searchRow2.Controls.Add(yahooSearchButton);
        catalogFlow.Controls.Add(searchRow2);
        // The whole built-in catalogue is listed to pick from; typing filters it at once, and Search asks Yahoo for anything else.
        yahooGroup = new FlatComboBox { Width = 240 };
        yahooGroup.SetItems(new[] { L("yahoo.group.all") }.Concat(YahooPresets.All.Select(g => L("yahoo.group." + g.Key))), 0);
        var groupRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        groupRow.Controls.Add(yahooGroup);
        catalogFlow.Controls.Add(groupRow);
        yahooHitList = new FlatList { Width = 740, Height = 30 * 10 + 4 };
        yahooHitList.SetShowChecks(true);
        catalogFlow.Controls.Add(yahooHitList);
        yahooAddButton = new FlatButton { Text = L("btn.yahooAdd") };
        yahooRemoveButton = new FlatButton { Text = L("btn.yahooRemove") };
        var buttonRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(740, 0) };
        buttonRow.Controls.Add(yahooAddButton);
        foreach (var group in YahooPresets.All)
        {
            var presetButton = new FlatButton { Text = L("yahoo.preset." + group.Key) };
            var g = group;
            presetButton.Click += (_, _) => AddYahoo(g.Items.Select(i => (i.Symbol, i.NameEl, i.NameEn)));
            yahooPresetButtons.Add(presetButton);
            buttonRow.Controls.Add(presetButton);
        }
        buttonRow.Controls.Add(yahooRemoveButton);
        catalogFlow.Controls.Add(buttonRow);
        yahooStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(700, 0), Margin = new Padding(0, 6, 0, 0), UseMnemonic = false };
        catalogFlow.Controls.Add(yahooStatus);
        catalog.Controls.Add(catalogFlow);
        flow.Controls.Add(catalog);
        yahooCatalog = catalog;
        yahooSearch.TextValueChanged += (_, _) => ShowYahooCatalogue();
        yahooGroup.SelectedIndexChanged += (_, _) => ShowYahooCatalogue();
        ShowYahooCatalogue();
        yahooSearchButton.Click += async (_, _) => await RunYahooSearchAsync();
        yahooAddButton.Click += (_, _) =>
            AddYahoo(Enumerable.Range(0, yahooHits.Count).Where(i => yahooHitList.GetChecked(i)).Select(i => (yahooHits[i].Symbol, yahooHits[i].Name, yahooHits[i].Name)));
        yahooRemoveButton.Click += (_, _) => RemoveSelectedYahoo();
        yahooHistory.CheckedChanged += (_, _) => UpdateYahooUi();
        UpdateYahooUi();
        flow.Controls.Add(Note(L("note.markets"), 700));
        flow.Controls.Add(Note(Loc.T("pk.data.limits.athens.prices"), 700));

        // All the shares of one Euronext market: pick the market, search, tick the ones to show.
        var card = new FlatCard(L("card.shares"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        marketCombo = new FlatComboBox { Width = 300 };
        marketCombo.SetItems(EuronextCatalog.Markets.Select(m => m.DisplayName), 0);
        refreshCatalog = new FlatButton { Text = L("btn.refreshList") };
        var marketRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        marketRow.Controls.Add(marketCombo);
        marketRow.Controls.Add(refreshCatalog);
        inner.Controls.Add(Row(L("lbl.market"), marketRow, 150));
        stockSearch = new FlatText { Width = 240 };
        var selectAll = new FlatButton { Text = L("btn.selectAll") };
        var selectNone = new FlatButton { Text = L("btn.selectNone") };
        var searchRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        searchRow.Controls.Add(stockSearch);
        searchRow.Controls.Add(selectAll);
        searchRow.Controls.Add(selectNone);
        selectAll.Click += (_, _) => SetAllVisibleStocks(true);
        selectNone.Click += (_, _) => SetAllVisibleStocks(false);
        inner.Controls.Add(Row(L("lbl.search"), searchRow, 150));
        stockList = new FlatList { Width = 740, Height = 340 };
        stockList.SetShowChecks(true);
        inner.Controls.Add(stockList);
        stockStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(680, 0), Margin = new Padding(0, 6, 0, 0) };
        inner.Controls.Add(stockStatus);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        marketCombo.SelectedIndexChanged += (_, _) =>
        {
            stockSearch.Value = "";
            LoadCatalog(force: false);
        };
        refreshCatalog.Click += (_, _) => LoadCatalog(force: true);
        stockSearch.TextValueChanged += (_, _) => ShowStocks();
        stockList.ItemChecked += (index, isChecked) => ToggleStock(index, isChecked);

        foreach (var (source, titleKey) in new[] { (QuoteSource.Metals, "card.metals"), (QuoteSource.Crypto, "card.crypto"), (QuoteSource.Currency, "card.currency"), (QuoteSource.Energy, "card.energy"), (QuoteSource.Yahoo, "card.yahoo") })
        {
            var small = new FlatCard(L(titleKey));
            var list = new FlatList { Width = 640, Height = 30 * 5 + 4 };
            list.SetShowChecks(true);
            var src = source;
            list.ItemChecked += (index, isChecked) =>
            {
                var items = workWatch.Where(i => i.Source == src).ToList();
                if (index < items.Count) items[index].Enabled = isChecked;
            };
            watchLists[source] = list;
            small.Controls.Add(list);
            flow.Controls.Add(small);
        }

        // Price alerts: a limit per instrument; when it is passed the bar shows an alert like a breaking headline.
        var alertCard = new FlatCard(Loc.T("pk.price.alerts"));
        var alertFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        alertFlow.Controls.Add(Note(Loc.T("pk.when.the.price.passes"), 640));
        alertList = new FlatList { Width = 640, Height = 30 * 4 + 4 };
        alertList.SetShowChecks(true);
        alertList.ItemChecked += (index, isChecked) =>
        {
            if (index >= 0 && index < workAlerts.Count) workAlerts[index].Enabled = isChecked;
        };
        alertFlow.Controls.Add(alertList);
        alertInstrument = new FlatComboBox { Width = 220 };
        alertDirection = new FlatComboBox { Width = 80 };
        alertDirection.SetItems(new[] { "≥", "≤" }, 0);
        alertMetric = new FlatComboBox { Width = 150 };
        alertMetric.SetItems(Loc.IsGreek ? new[] { "Τιμή", "Μεταβολή %" } : new[] { "Price", "Change %" }, 0);
        alertThreshold = new FlatText { Width = 100 };
        var addAlert = new FlatButton { Name = "addPriceAlert", Text = L("btn.new") };
        addAlert.Click += (_, _) => AddAlert();
        var removeAlert = new FlatButton { Name = "removePriceAlert", Text = L("btn.delete") };
        removeAlert.Click += (_, _) => RemoveAlert();
        var alertRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(660, 0) };   // wraps instead of making the card wider than the page
        alertRow.Controls.AddRange(new Control[] { alertInstrument, alertMetric, alertDirection, alertThreshold, addAlert, removeAlert });
        alertFlow.Controls.Add(alertRow);
        alertMargin = new FlatText { Width = 100 }; alertMargin.Value = "0";
        alertAge = new FlatNumber(); alertAge.Configure(0, 10080, 1);
        // Draft fields only become settings when the Add action creates an alert.
        foreach (var draft in new Control[] { alertInstrument, alertMetric, alertDirection, alertThreshold, alertMargin, alertAge, addAlert, removeAlert })
            transientControls.Add(draft);
        alertFlow.Controls.Add(Row(Loc.IsGreek ? "Περιθώριο επανόπλισης (μονάδες ή ποσοστιαίες μονάδες)" : "Re-arm margin (units or percentage points)", alertMargin, 330));
        alertFlow.Controls.Add(Row(Loc.IsGreek ? "Μέγιστη ηλικία τιμής (λεπτά, 0 = χωρίς όριο)" : "Maximum quote age (minutes, 0 = unlimited)", alertAge, 330));
        alertCard.Controls.Add(alertFlow);
        flow.Controls.Add(alertCard);

        // Which price sources answered, how many prices and how fresh (updates by itself while the window is open).
        var health = new FlatCard(Loc.T("pk.price.source.status"));
        sourceStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(680, 0) };
        health.Controls.Add(sourceStatus);
        flow.Controls.Add(health);
        sourceTimer.Tick -= UpdateSourceStatus;
        sourceTimer.Tick += UpdateSourceStatus;
        if (Visible) sourceTimer.Start();
        UpdateSourceStatus(null, EventArgs.Empty);
        return page;
    }

    /// <summary>The instruments an alert can be set for: the ticked ones. Rebuilt only when that set changes.</summary>
    private void RefreshAlertChoices()
    {
        if (alertInstrument is null || alertInstrument.IsDisposed) return;
        var choices = workWatch.Where(w => w.Enabled).ToList();
        var signature = string.Join("|", choices.Select(c => c.Source + ":" + c.Market + ":" + c.Code));
        if (signature == alertChoicesSignature && alertInstrument.Count == choices.Count) return;
        alertChoicesSignature = signature;
        alertChoices = choices;
        alertInstrument.SetItems(choices.Select(c => string.IsNullOrEmpty(c.Symbol) ? c.DisplayName(Loc.IsGreek) : c.Symbol + " — " + c.DisplayName(Loc.IsGreek)), choices.Count > 0 ? 0 : -1);
    }

    private void ShowAlerts()
    {
        if (alertList is null || alertList.IsDisposed) return;
        alertList.ClearRows();
        foreach (var a in workAlerts)
            alertList.AddRow($"{a.Label}  {(a.Above ? "≥" : "≤")}  {a.Threshold.ToString("0.00##", System.Globalization.CultureInfo.CurrentCulture)}{(a.Metric == "changePercent" ? "%" : "")} · Δ {a.RearmMargin:0.####} · {(a.MaxAgeMinutes == 0 ? "∞" : a.MaxAgeMinutes.ToString()+" min")}", a.Enabled);
    }

    private void AddAlert()
    {
        RefreshAlertChoices();
        var index = alertInstrument.SelectedIndex;
        if (index < 0 || index >= alertChoices.Count) return;
        var text = (alertThreshold.Value ?? "").Trim().Replace(',', '.');
        if (!decimal.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var limit)
            || (alertMetric.SelectedIndex == 0 ? limit <= 0 : limit is < -100 or > 100))
        {
            SetStatus(Loc.T("pk.enter.a.valid.limit"));
            return;
        }
        var marginText = (alertMargin.Value ?? "0").Trim().Replace(',', '.');
        if (!decimal.TryParse(marginText, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var margin) || margin < 0 || margin > 1_000_000m)
        { SetStatus(Loc.IsGreek ? "Μη έγκυρο περιθώριο επανόπλισης." : "Invalid re-arm margin."); return; }
        var inst = alertChoices[index];
        var metric = alertMetric.SelectedIndex == 1 ? "changePercent" : "price";
        var above = alertDirection.SelectedIndex != 1;
        if (workAlerts.Count >= 200 || workAlerts.Any(a => a.Matches(inst) && a.Metric == metric && a.Above == above && a.Threshold == limit))
        { SetStatus(Loc.IsGreek ? "Διπλότυπος κανόνας ή όριο 200 ειδοποιήσεων." : "Duplicate rule or 200-alert limit."); return; }
        workAlerts.Add(new PriceAlert
        {
            Source = inst.Source, Code = inst.Code, Market = inst.Market,
            Label = string.IsNullOrEmpty(inst.Symbol) ? inst.DisplayName(Loc.IsGreek) : inst.Symbol,
            Above = alertDirection.SelectedIndex != 1, Threshold = limit, Enabled = true,
            Metric = metric, RearmMargin = margin, MaxAgeMinutes = alertAge.Value
        });
        alertThreshold.Value = "";
        ShowAlerts();
        MarkDirty();
    }

    private void RemoveAlert()
    {
        var index = alertList.SelectedIndex;
        if (index < 0 || index >= workAlerts.Count) return;
        workAlerts.RemoveAt(index);
        ShowAlerts();
        MarkDirty();
    }

    private static string SourceName(QuoteSource source) => source switch
    {
        QuoteSource.Athens => Loc.T("pk.euronext.athens.15.min"),
        QuoteSource.Euronext => Loc.T("pk.other.euronext.markets.end"),
        QuoteSource.Metals => Loc.T("pk.metals.gold.api.com"),
        QuoteSource.Crypto => Loc.T("pk.crypto.gold.api.com"),
        QuoteSource.Currency => Loc.T("pk.currencies.ecb.once.a"),
        QuoteSource.Energy => Loc.T("pk.energy.pyth.fred.fallback"),
        QuoteSource.Yahoo => "Yahoo Finance",
        _ => source.ToString()
    };

    private void UpdateSourceStatus(object? sender, EventArgs e)
    {
        if (sourceStatus is null || sourceStatus.IsDisposed) return;
        RefreshAlertChoices();
        var rows = QuoteService.Statuses();
        if (rows.Count == 0)
        {
            sourceStatus.Text = Loc.T("pk.no.prices.have.been");
            return;
        }
        var lines = rows.Select(s =>
        {
            var state = s.Failed ? Loc.T("pk.failed") : Loc.T("pk.ok");
            var line = $"{(s.Failed ? "✖" : "✔")} {SourceName(s.Source)}: {state}, {s.Count} {Loc.T("pk.prices")}, "
                + $"{Loc.T("pk.checked")} {s.CheckedAt:HH:mm:ss}";
            if (s.Newest is { } newest) line += $", {Loc.T("pk.newest.price")} {newest.ToLocalTime():dd/MM HH:mm}";
            return line;
        });
        sourceStatus.Text = string.Join(Environment.NewLine, lines);
    }
}

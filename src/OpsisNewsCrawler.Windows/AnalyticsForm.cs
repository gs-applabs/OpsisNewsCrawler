using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// One window for the 0.30.0 analytics (N07, N09, N11-N16, N19-N30, N39). Every view only reads data the program already holds (news in memory or the
/// opt-in local archive, stored prices, the ledger, the imported calendar). Nothing is fetched here and nothing is investment advice.
/// </summary>
public sealed class AnalyticsForm : Form
{
    private enum View { Entities, Relations, Coverage, Trend, Places, Mosaic, Heatmap, Risk, Allocation, NearMoves, Calendar, Search, Changes }

    private readonly TickerHost host;
    private readonly FlatComboBox views = new() { Width = 300 };
    private readonly FlatText box = new() { Width = 240 };
    private readonly FlatText benchmark = new() { Width = 180 };
    private readonly FlatComboBox category = new() { Width = 190 };
    private readonly CheckBox archive = new() { AutoSize = true, Margin = new Padding(8, 8, 8, 0) };
    private readonly ListView lines = new() { Dock = DockStyle.Fill, View = System.Windows.Forms.View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
    private readonly Panel canvas = new() { Dock = DockStyle.Fill, Visible = false, AutoScroll = true };
    private readonly Label note = new() { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8, 4, 8, 4) };
    private readonly List<string?> links = new();
    private readonly List<(string Title, string Detail, double? Value)> tiles = new();

    // the data of the drawn views (N11-N14)
    private IReadOnlyList<PlaceCount> places = Array.Empty<PlaceCount>();
    private IReadOnlyList<EntityLink> relations = Array.Empty<EntityLink>();
    private IReadOnlyList<TrendBucket> trend = Array.Empty<TrendBucket>();
    private View drawing;

    private static string T(string el, string en) => Loc.IsGreek ? el : en;

    public AnalyticsForm(TickerHost host)
    {
        this.host = host;
        Text = T("Αναλύσεις — Opsis News Crawler", "Analytics — Opsis News Crawler");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1040, 660);
        MinimumSize = new Size(720, 460);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        views.SetItems(new[]
        {
            T("Πρόσωπα και οργανισμοί", "People and organisations"), T("Συσχετίσεις", "Relations"), T("Κάλυψη ιστοριών", "Story coverage"),
            T("Τάση όρου", "Term trend"), T("Γεωγραφία (δημοσιεύματα)", "Geography (publications)"), T("Ψηφιδωτό ειδήσεων", "News mosaic"),
            T("Χάρτης αγορών", "Market heatmap"), T("Κίνδυνος: μεταβλητότητα, υποχώρηση", "Risk: volatility, drawdown"),
            T("Κατανομή χαρτοφυλακίου και απόδοση", "Portfolio allocation and return"), T("Ειδήσεις γύρω από κινήσεις", "News around price moves"),
            T("Ημερολόγιο: αποτελέσματα, μερίσματα, δείκτες", "Calendar: results, dividends, indicators"),
            T("Αναζήτηση αρχείου", "Archive search"), T("Αλλαγές τίτλων", "Headline changes")
        }, 0);
        views.SelectedIndexChanged += (_, _) => Reload();
        box.TextValueChanged += (_, _) => Reload();
        benchmark.TextValueChanged += (_, _) => Reload();
        box.AccessibleName = T("Όρος αναζήτησης", "Search term");
        benchmark.AccessibleName = T("Δείκτης σύγκρισης (κωδικός τιμής)", "Benchmark (price code)");
        category.SetItems(new[] { T("Όλα", "All"), T("Αποτελέσματα", "Results"), T("Μέρισμα (αποκοπή)", "Dividend (ex-date)"), T("Μέρισμα (πληρωμή)", "Dividend (payment)"), T("Μακροοικονομικά", "Macro") }, 0);
        category.SelectedIndexChanged += (_, _) => Reload();
        archive.Text = T("Τοπικό αρχείο ειδήσεων (30 ημέρες, έως 5.000)", "Local news archive (30 days, up to 5,000)");
        archive.Checked = host.Config.ArchiveNews;
        archive.CheckedChanged += (_, _) => SetArchive(archive.Checked);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(10, 8, 8, 0), WrapContents = true };
        bar.Controls.AddRange(new Control[] { views, box, benchmark, category, archive });
        lines.Columns.Add(T("Στοιχείο", "Item"), 520);
        lines.Columns.Add(T("Στοιχεία", "Figures"), 460);
        lines.AccessibleName = T("Αποτελέσματα ανάλυσης", "Analytics results");
        lines.OwnerDraw = true;   // the column headers follow the theme too
        lines.DrawColumnHeader += (_, e) =>
        {
            using var back = new SolidBrush(SystemInformation.HighContrast ? SystemColors.Control : Theme.Background);
            e.Graphics.FillRectangle(back, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text, Font, Rectangle.Inflate(e.Bounds, -6, 0), SystemInformation.HighContrast ? SystemColors.ControlText : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };
        lines.DrawItem += (_, e) => e.DrawDefault = true;
        lines.DrawSubItem += (_, e) => e.DrawDefault = true;
        lines.DoubleClick += (_, _) => OpenSelected();
        lines.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; } };
        canvas.Paint += PaintTiles;
        Controls.Add(lines); Controls.Add(canvas); Controls.Add(note); Controls.Add(bar);
        Theme.Changed += ApplyLook;
        FormClosed += (_, _) => Theme.Changed -= ApplyLook;
        Shown += (_, _) => { ApplyLook(); Reload(); };
    }

    private void ApplyLook()
    {
        if (IsDisposed) return;
        BackColor = Theme.Background; ForeColor = Theme.Text;
        lines.BackColor = canvas.BackColor = Theme.Surface; lines.ForeColor = Theme.Text;
        note.ForeColor = Theme.TextMuted; archive.ForeColor = Theme.Text;
        canvas.Invalidate();
    }

    private void SetArchive(bool on)
    {
        if (host.Config.ArchiveNews == on) return;
        host.Config.ArchiveNews = on;
        if (!host.TrySaveConfig()) { host.Config.ArchiveNews = !on; archive.Checked = !on; note.Text = T("Η ρύθμιση δεν αποθηκεύτηκε.", "The setting could not be saved."); return; }
        if (on) host.Archive.Observe(host.RecentNews, DateTimeOffset.UtcNow);
        else host.Archive.Clear();   // switching it off removes what was kept
        Reload();
    }

    private void Add(string name, string detail, string? url = null)
    {
        lines.Items.Add(new ListViewItem(new[] { name, detail }));
        links.Add(url);
    }

    internal static int ViewCount => Enum.GetValues<View>().Length;

    internal void ShowView(int index) { views.SelectedIndex = index; Reload(); }

    internal void Reload()
    {
        if (IsDisposed) return;
        var view = (View)Math.Max(0, views.SelectedIndex);
        box.Visible = view is View.Trend or View.Search or View.NearMoves;
        benchmark.Visible = view == View.Risk;
        category.Visible = view == View.Calendar;
        archive.Visible = view is View.Search or View.Changes;
        var drawn = view is View.Heatmap or View.Mosaic;                       // tiles only
        var picture = view is View.Places or View.Relations or View.Trend;      // a drawing above the list of evidence
        drawing = view;
        canvas.Visible = drawn || picture; lines.Visible = !drawn;
        lines.Dock = picture ? DockStyle.Bottom : DockStyle.Fill;
        // docking runs from the back of the z-order: the list must dock before the drawing that fills the rest
        Controls.SetChildIndex(canvas, 0);
        Controls.SetChildIndex(lines, picture ? 1 : 0);
        if (picture) lines.Height = Math.Max(140, ClientSize.Height * 2 / 5);
        places = Array.Empty<PlaceCount>(); relations = Array.Empty<EntityLink>(); trend = Array.Empty<TrendBucket>();
        lines.BeginUpdate();
        lines.Items.Clear(); links.Clear(); tiles.Clear();
        var news = host.RecentNews;
        note.Text = T("Μετρήσεις δημοσιευμάτων και αποθηκευμένων τιμών. Δεν αποτελούν επαλήθευση ούτε επενδυτική σύσταση.",
            "Counts of publications and stored prices. Not verification and not investment advice.");
        try
        {
            switch (view)
            {
                case View.Entities:
                    foreach (var e in NewsAnalytics.Entities(news))
                        Add(e.Name, T($"{e.Reports} τίτλοι · {e.Publishers} εκδότες", $"{e.Reports} headlines · {e.Publishers} publishers"), e.Links.FirstOrDefault());
                    note.Text = T("Ονόματα από κεφαλαία στους τίτλους: ενδεικτικά, όχι επιβεβαιωμένες ταυτότητες.", "Capitalised names in headlines: a text pattern, not verified identities.");
                    break;
                case View.Relations:
                    relations = NewsAnalytics.Relations(news);
                    foreach (var l in relations) Add($"{l.A}  ↔  {l.B}", T($"{l.Reports} κοινοί τίτλοι", $"{l.Reports} shared headlines"), l.Links.FirstOrDefault());
                    break;
                case View.Coverage:
                    foreach (var c in NewsAnalytics.Coverage(news)) Add(c.Headline, T($"{c.Reports} δημοσιεύματα · {c.Publishers} ομάδες · {c.Latest.ToLocalTime():dd/MM HH:mm}", $"{c.Reports} reports · {c.Publishers} groups · {c.Latest.ToLocalTime():dd/MM HH:mm}"));
                    break;
                case View.Trend:
                    trend = NewsAnalytics.Trend(news, box.Value, DateTimeOffset.Now);
                    foreach (var t in trend)
                        Add(t.Start.ToLocalTime().ToString("dd/MM HH:mm"), new string('■', Math.Min(40, t.Reports)) + $"  {t.Reports} / {t.Publishers}");
                    note.Text = string.IsNullOrWhiteSpace(box.Value)
                        ? T("Γράψτε έναν όρο στο πεδίο.", "Type a term in the box.")
                        : T("Πλήθος τίτλων ανά δίωρο για τον όρο (τίτλοι / εκδότες).", "Headlines per two hours naming the term (headlines / publishers).");
                    break;
                case View.Places:
                    places = NewsAnalytics.Places(news);
                    foreach (var g in places.GroupBy(p => p.ContinentKey).OrderByDescending(g => g.Sum(p => p.Reports)))
                    {
                        Add("▸ " + T("Ήπειρος", "Continent") + ": " + Loc.T("continent." + g.Key), g.Sum(p => p.Reports).ToString());
                        foreach (var p in g) Add("     " + p.CountryName, new string('■', Math.Min(40, p.Reports)) + "  " + p.Reports);
                    }
                    note.Text = T("Δημοσιεύματα που ονομάζουν μία μόνο χώρα. Δεν δείχνει πόσο συχνά γίνονται γεγονότα εκεί.", "Publications naming exactly one country. It does not say how often things happen there.");
                    break;
                case View.Mosaic:
                    foreach (var c in NewsAnalytics.Coverage(news, 40)) tiles.Add((c.Headline, T($"ομάδες εκδοτών: {c.Publishers}", $"publisher groups: {c.Publishers}"), null));
                    break;
                case View.Heatmap:
                    foreach (var (inst, quote) in host.QuoteService.LastQuotes.Take(80))
                        tiles.Add((inst.Symbol.Length > 0 ? inst.Symbol : inst.DisplayName(Loc.IsGreek),
                            quote.ChangePercent is { } p ? $"{p:+0.00;-0.00;0.00}%" + (quote.Delayed ? " · " + T("καθυστέρηση", "delayed") : "") : T("χωρίς μεταβολή", "no change data"),
                            (double?)quote.ChangePercent));
                    note.Text = T("Μεταβολή ημέρας στο νόμισμα του κάθε τίτλου· γκρι = λείπουν δεδομένα.", "Day change in each instrument's own currency; grey = no data.");
                    break;
                case View.Risk: FillRisk(); break;
                case View.Allocation: FillAllocation(); break;
                case View.NearMoves: FillNearMoves(news); break;
                case View.Calendar:
                    var wanted = category.SelectedIndex switch { 1 => "results", 2 => "dividend-ex", 3 => "dividend-pay", 4 => "macro", _ => null };
                    foreach (var e in EconomicCalendar.Select(host.Config.EconomicEvents, DateTimeOffset.Now, 90, 0, null, category: wanted))
                        Add(e.Title, $"{e.StartsAt.ToLocalTime():dd/MM/yyyy HH:mm} · {e.Country} · {e.Source}" + (e.Period is { } per ? " · " + per : "") +
                            $" · A {e.Actual ?? "—"} / F {e.Forecast ?? "—"} / P {e.Previous ?? "—"}" + (e.Unit is { } u ? " " + u : "") + (e.Revised is { } r ? T(" · αναθεώρηση ", " · revised ") + r : ""), e.Url);
                    note.Text = T("Μόνο εγγραφές που εισαγάγατε (αρχείο JSON του ημερολογίου), όχι ζωντανή ροή.", "Only entries you imported (the calendar JSON file), not a live feed.");
                    break;
                case View.Search:
                    if (!host.Config.ArchiveNews) note.Text = T("Το αρχείο είναι κλειστό: εμφανίζονται μόνο οι τρέχουσες ειδήσεις.", "The archive is off: only the current news are searched.");
                    var query = new NewsQuery(box.Value);
                    foreach (var n in host.Config.ArchiveNews ? host.Archive.Search(query) : NewsSearch.Find(news, query)) Add(n.Title, $"{n.Source} · {n.PublishedAt.ToLocalTime():dd/MM HH:mm}", n.Url);
                    break;
                case View.Changes:
                    foreach (var c in host.Config.ArchiveNews ? host.Archive.Changes : Array.Empty<ArticleChange>())
                        Add(c.NewTitle, T($"ήταν: {c.OldTitle} · {c.SeenAt.ToLocalTime():dd/MM HH:mm}", $"was: {c.OldTitle} · {c.SeenAt.ToLocalTime():dd/MM HH:mm}"), c.Url);
                    note.Text = T("Αλλαγές τίτλου ή περίληψης στην ίδια διεύθυνση, όπως τις έδωσε η τροφοδοσία (απαιτεί το τοπικό αρχείο).",
                        "Headline or summary changes at the same address, as the feed delivered them (needs the local archive).");
                    break;
            }
            if (drawn && tiles.Count == 0) { drawn = false; canvas.Visible = false; lines.Visible = true; }   // say so instead of showing an empty drawing
            if (!drawn && lines.Items.Count == 0) Add(T("Δεν υπάρχουν αρκετά δεδομένα.", "Not enough data."), "");
        }
        finally { lines.EndUpdate(); }
        canvas.AutoScrollMinSize = drawn ? new Size(0, TileRows() * TileHeight + 12) : Size.Empty;
        if (canvas.Visible) canvas.Invalidate();
        FitColumns();
    }

    private IReadOnlyList<DatedClose> SeriesOf(string storeKey) => MarketMetrics.Series(host.QuoteService.Store.Candles(storeKey));

    private IEnumerable<(string Key, string Label)> Instruments() =>
        host.Config.Portfolio.Select(p => (p.StoreKey, p.DisplayName(Loc.IsGreek)))
            .Concat(host.Config.Watchlist.Select(w => (w.StoreKey, w.Symbol.Length > 0 ? w.Symbol : w.DisplayName(Loc.IsGreek))))
            .GroupBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).Select(g => (g.Key, g.First().Item2));

    private void FillRisk()
    {
        var bench = string.IsNullOrWhiteSpace(benchmark.Value) ? Array.Empty<DatedClose>() : SeriesOf(benchmark.Value.Trim());
        foreach (var (key, label) in Instruments().Take(60))
        {
            var series = SeriesOf(key);
            var vol = MarketMetrics.AnnualisedVolatility(series);
            var dd = MarketMetrics.MaxDrawdown(series);
            var text = vol is null ? T($"λίγα δείγματα ({series.Count})", $"too few samples ({series.Count})")
                : T($"μεταβλητότητα {vol.AnnualisedPercent:0.0}% (ετησ., {vol.Returns} αποδ.)", $"volatility {vol.AnnualisedPercent:0.0}% (annualised, {vol.Returns} returns)");
            if (dd is not null) text += T($" · μέγιστη υποχώρηση {dd.Percent:0.0}%", $" · max drawdown {dd.Percent:0.0}%");
            if (bench.Count > 0 && MarketMetrics.Compare(series, bench) is { } c)
                text += T($" · τιμή {c.SeriesPercent:+0.0;-0.0}% έναντι δείκτη {c.BenchmarkPercent:+0.0;-0.0}% ({c.CommonDays} ημ.)", $" · price {c.SeriesPercent:+0.0;-0.0}% vs benchmark {c.BenchmarkPercent:+0.0;-0.0}% ({c.CommonDays} days)");
            Add(label, text);
        }
        note.Text = T("Από τις αποθηκευμένες τιμές κλεισίματος· μόνο μεταβολή τιμής (όχι μερίσματα). Δώστε κωδικό τιμής για σύγκριση ίδιας περιόδου.",
            "From stored closing prices; price change only (no dividends). Type a price code to compare the same period.");
    }

    private void FillAllocation()
    {
        var holdings = host.Config.Portfolio.Select(p =>
        {
            var series = SeriesOf(p.StoreKey);
            return (p.DisplayName(Loc.IsGreek), p.Currency, series.Count > 0 ? (decimal?)(series[^1].Close * p.Quantity) : null);
        });
        var result = MarketMetrics.Allocation(holdings);
        foreach (var r in result.Rows) Add(r.Name, $"{r.Value:0.00} {r.Currency} · {r.PercentOfCurrency:0.0}% " + T("του νομίσματος", "of its currency"));
        if (result.Unvalued > 0) Add(T("Χωρίς διαθέσιμη τιμή", "No price available"), result.Unvalued.ToString());
        foreach (var group in host.Config.PortfolioTransactions.GroupBy(t => (Key: (t.InstrumentKey ?? "").Trim().ToUpperInvariant(), Currency: (t.Currency ?? "").Trim().ToUpperInvariant())).Take(40))
        {
            var holding = host.Config.Portfolio.FirstOrDefault(p => string.Equals(p.Key, group.Key.Key, StringComparison.OrdinalIgnoreCase));
            var storeKey = holding?.StoreKey ?? group.Key.Key.Split('|').LastOrDefault() ?? "";
            var points = MarketMetrics.LedgerValuations(group, SeriesOf(storeKey));
            var twr = points is null ? null : MarketMetrics.TimeWeightedReturnPercent(points);
            Add($"TWR · {group.Key.Key} · {group.Key.Currency}", twr is { } v ? $"{v:+0.00;-0.00;0.00}%" : T("μη διαθέσιμη (λείπουν τιμές ή υπάρχει split)", "unavailable (prices missing or a split)"));
        }
        note.Text = T("Η αξία είναι ποσότητα × τελευταία αποθηκευμένη τιμή· ποτέ άθροισμα διαφορετικών νομισμάτων. Η TWR χρειάζεται τιμή σε κάθε ημέρα συναλλαγής.",
            "Value = quantity × last stored price; currencies are never added together. TWR needs a price on every trade day.");
    }

    private void FillNearMoves(IReadOnlyList<NewsItem> news)
    {
        foreach (var (inst, quote) in host.QuoteService.LastQuotes.Where(q => Math.Abs((double)(q.Quote.ChangePercent ?? 0)) >= 3).Take(20))
        {
            var name = string.IsNullOrWhiteSpace(box.Value) ? inst.DisplayName(Loc.IsGreek) : box.Value;
            var near = NewsAnalytics.NearMove(news, name, quote.AsOf);
            Add($"▸ {inst.DisplayName(Loc.IsGreek)}  {quote.ChangePercent:+0.00;-0.00}%", T($"{near.Count} ειδήσεις με το όνομα ±24 ώρες", $"{near.Count} headlines naming it within ±24 hours"));
            foreach (var n in near.Take(5)) Add("     " + n.Title, $"{n.Source} · {n.PublishedAt.ToLocalTime():dd/MM HH:mm}", n.Url);
        }
        note.Text = T("Κινήσεις ≥ 3% και ειδήσεις που αναφέρουν το όνομα στο ίδιο χρονικό παράθυρο: σύμπτωση στον χρόνο, όχι αιτία.",
            "Moves of 3% or more and headlines naming the instrument in the same window: coincidence in time, not a cause.");
    }

    private int TileHeight => Math.Max(92, Font.Height * 5);

    private int TileColumns() => Math.Max(1, (canvas.ClientSize.Width - 12) / 190);
    private int TileRows() => (tiles.Count + TileColumns() - 1) / TileColumns();

    private void PaintTiles(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (drawing == View.Places) { PaintMap(g); return; }
        if (drawing == View.Relations) { PaintGraph(g); return; }
        if (drawing == View.Trend) { PaintTrend(g); return; }
        g.TranslateTransform(0, canvas.AutoScrollPosition.Y);
        var columns = TileColumns();
        var width = (canvas.ClientSize.Width - 12) / columns;
        using var bold = new Font(Font, FontStyle.Bold);
        for (var i = 0; i < tiles.Count; i++)
        {
            var r = new Rectangle(8 + i % columns * width, 8 + i / columns * TileHeight, width - 8, TileHeight - 8);
            var (title, detail, value) = tiles[i];
            // green up, red down, grey unknown; the strength follows the size of the move, and the text is always there (not colour alone)
            var strength = value is { } v ? Math.Min(1.0, Math.Abs(v) / 5.0) : 0;
            var back = value is null ? Theme.Background
                : Blend(Theme.Background, value >= 0 ? Color.SeaGreen : Color.Firebrick, 0.2 + 0.6 * strength);
            if (SystemInformation.HighContrast) back = SystemColors.Window;
            using var brush = new SolidBrush(back);
            g.FillRectangle(brush, r);
            var text = SystemInformation.HighContrast ? SystemColors.WindowText : Theme.Text;
            TextRenderer.DrawText(g, title, bold, new Rectangle(r.X + 6, r.Y + 4, r.Width - 12, r.Height - 28), text, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsTranslateTransform);
            TextRenderer.DrawText(g, detail, Font, new Rectangle(r.X + 6, r.Bottom - 22, r.Width - 12, 18), text, TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsTranslateTransform);
        }
    }

    private Color Ink => SystemInformation.HighContrast ? SystemColors.WindowText : Theme.Text;

    /// <summary>N11/N12: the continent outlines the weather pages already use, shaded by the number of publications that name a country there.</summary>
    private void PaintMap(Graphics g)
    {
        var area = canvas.ClientRectangle;
        if (area.Width < 40 || area.Height < 40) return;
        var counts = places.GroupBy(p => p.ContinentKey).ToDictionary(x => x.Key, x => x.Sum(p => p.Reports), StringComparer.OrdinalIgnoreCase);
        var most = Math.Max(1, counts.Values.DefaultIfEmpty(0).Max());
        var scale = Math.Min((area.Width - 16) / 360f, (area.Height - 16) / 180f);
        var left = (area.Width - 360 * scale) / 2;
        var top = (area.Height - 180 * scale) / 2;
        PointF At(double lon, double lat) => new(left + (float)(lon + 180) * scale, top + (float)(90 - lat) * scale);
        using var pen = new Pen(Theme.Border);
        foreach (var continent in Continents.Order)
        {
            var key = Continents.Key(continent);
            counts.TryGetValue(key, out var n);
            var fill = n == 0 || SystemInformation.HighContrast ? Theme.Background : Blend(Theme.Background, Theme.Accent, 0.25 + 0.65 * n / most);
            var all = new List<PointF>();
            foreach (var outline in Continents.Outline(continent))
            {
                var points = outline.Select(p => At(p.Lon, p.Lat)).ToArray();
                if (points.Length < 3) continue;
                using var brush = new SolidBrush(fill);
                g.FillPolygon(brush, points);
                g.DrawPolygon(pen, points);
                all.AddRange(points);
            }
            if (n > 0 && all.Count > 0)   // the number is written too, so the meaning never depends on colour alone
                TextRenderer.DrawText(g, Loc.T("continent." + key) + ": " + n, Font, Point.Round(new PointF(all.Average(p => p.X) - 40, all.Average(p => p.Y) - 8)), Ink);
        }
    }

    /// <summary>N13: names on a circle, a line for every pair that shares headlines (wider = more).</summary>
    private void PaintGraph(Graphics g)
    {
        var names = relations.SelectMany(r => new[] { r.A, r.B }).Distinct().Take(14).ToList();
        if (names.Count == 0) return;
        var area = canvas.ClientRectangle;
        var centre = new PointF(area.Width / 2f, area.Height / 2f);
        var rx = Math.Max(60, area.Width / 2f - 140);
        var ry = Math.Max(40, area.Height / 2f - 30);
        var at = names.Select((n, i) => (n, p: new PointF(centre.X + rx * (float)Math.Cos(2 * Math.PI * i / names.Count), centre.Y + ry * (float)Math.Sin(2 * Math.PI * i / names.Count))))
            .ToDictionary(x => x.n, x => x.p);
        foreach (var link in relations)
        {
            if (!at.TryGetValue(link.A, out var a) || !at.TryGetValue(link.B, out var b)) continue;
            using var pen = new Pen(Theme.Accent, Math.Min(6, 1 + link.Reports / 2f));
            g.DrawLine(pen, a, b);
        }
        foreach (var (name, p) in at)
        {
            using var dot = new SolidBrush(Ink);
            g.FillEllipse(dot, p.X - 4, p.Y - 4, 8, 8);
            var size = TextRenderer.MeasureText(name, Font);
            var x = p.X < centre.X ? p.X - size.Width - 4 : p.X + 6;
            TextRenderer.DrawText(g, name, Font, new Point((int)Math.Clamp(x, 0, Math.Max(0, area.Width - size.Width)), (int)p.Y - size.Height / 2), Ink);
        }
    }

    /// <summary>N14: headlines per time bucket as bars, with the count on each.</summary>
    private void PaintTrend(Graphics g)
    {
        if (trend.Count == 0) return;
        var area = Rectangle.Inflate(canvas.ClientRectangle, -16, -24);
        if (area.Width < 40 || area.Height < 30) return;
        var most = Math.Max(1, trend.Max(t => t.Reports));
        var step = area.Width / (float)trend.Count;
        for (var i = 0; i < trend.Count; i++)
        {
            var height = area.Height * trend[i].Reports / (float)most;
            var bar = new RectangleF(area.Left + i * step + 4, area.Bottom - height, Math.Max(4, step - 8), height);
            using var brush = new SolidBrush(Theme.Accent);
            g.FillRectangle(brush, bar);
            TextRenderer.DrawText(g, trend[i].Reports.ToString(), Font, new Point((int)bar.X, (int)Math.Max(0, bar.Y - 18)), Ink);
            TextRenderer.DrawText(g, trend[i].Start.ToLocalTime().ToString("HH:mm"), Font, new Point((int)bar.X, area.Bottom + 2), Ink);
        }
    }

    /// <summary>The second column takes the rest of the width (no bare header corner).</summary>
    private void FitColumns()
    {
        if (lines.Columns.Count == 2) lines.Columns[1].Width = Math.Max(200, lines.ClientSize.Width - lines.Columns[0].Width - 2);
    }

    private static Color Blend(Color a, Color b, double t) =>
        Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (!IsHandleCreated) return;
        if (drawing is View.Heatmap or View.Mosaic) canvas.AutoScrollMinSize = new Size(0, TileRows() * TileHeight + 12);
        else if (canvas.Visible) lines.Height = Math.Max(140, ClientSize.Height * 2 / 5);
        canvas.Invalidate();
        FitColumns();
    }

    private void OpenSelected()
    {
        var index = lines.SelectedIndices.Count > 0 ? lines.SelectedIndices[0] : -1;
        if (index < 0 || index >= links.Count || !NetGuard.IsPublicHttpUrl(links[index])) return;
        try { Process.Start(new ProcessStartInfo(links[index]!) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
    }
}

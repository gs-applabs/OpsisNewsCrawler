using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 extracted Sources settings pages. Shares the existing instance fields and theme infrastructure.</summary>
public sealed partial class SettingsPanel
{
    private Control BuildSources()
    {
        var page = new StableScrollPanel { Padding = new Padding(8, 6, 8, 6) };
        var root = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };

        var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 18, 0) };
        left.Controls.Add(new Label { Text = L("lbl.categories"), AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
        categoryList = new FlatList { Width = 240, Height = 260 };
        left.Controls.Add(categoryList);
        var catButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(260, 0) };
        var addCat = new FlatButton { Text = L("btn.new") };
        var renCat = new FlatButton { Text = L("btn.rename") };
        var delCat = new FlatButton { Text = L("btn.delete") };
        addCat.Click += (_, _) => AddCategory();
        renCat.Click += (_, _) => RenameCategory();
        delCat.Click += (_, _) => DeleteCategory();
        catButtons.Controls.AddRange(new Control[] { addCat, renCat, delCat });
        left.Controls.Add(catButtons);

        var right = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        right.Controls.Add(new Label { Text = L("lbl.sites"), AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
        feedList = new FlatList { Width = 520, Height = 260 };
        feedList.SetShowChecks(true);
        right.Controls.Add(feedList);
        var sourceActions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(520, 0) };
        var removeFeed = new FlatButton { Text = L("btn.removeFeed") };
        removeFeed.Click += (_, _) => RemoveFeed();
        var importOpml = new FlatButton { Text = Loc.T("pk.import.opml") };
        importOpml.Click += (_, _) => ImportOpml();
        var exportOpml = new FlatButton { Text = Loc.T("pk.export.opml") };
        exportOpml.Click += (_, _) => ExportOpml();
        var testFeed = new FlatButton { Text = Loc.T("pk.test") };
        testFeed.Click += (_, _) => TestFeeds(selectedOnly: true);
        var testAll = new FlatButton { Text = Loc.T("pk.test.all") };
        testAll.Click += (_, _) => TestFeeds(selectedOnly: false);
        var moveUp = new FlatButton { Text = "▲" };
        moveUp.Click += (_, _) => MoveFeed(-1);
        var moveDown = new FlatButton { Text = "▼" };
        moveDown.Click += (_, _) => MoveFeed(1);
        sourceActions.Controls.AddRange(new Control[] { removeFeed, moveUp, moveDown, testFeed, testAll, importOpml, exportOpml });
        right.Controls.Add(sourceActions);

        var newsOptions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 8, 0, 4) };
        newsOptions.Controls.Add(new Label { Text = Loc.T("pk.maximum.story.age.hours"), AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        newsMaxAge = new FlatNumber();
        newsMaxAge.Configure(1, 720, 24);
        newsOptions.Controls.Add(newsMaxAge);
        newsNewestFirst = new FlatCheckBox { Text = Loc.T("pk.newest.stories.first"), AutoSize = true, Margin = new Padding(8, 5, 0, 0) };
        newsOptions.Controls.Add(newsNewestFirst);
        stockNewsOnly = new FlatCheckBox { Text = Loc.T("pk.only.news.about.my"), AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
        right.Controls.Add(newsOptions);
        right.Controls.Add(stockNewsOnly);
        right.Controls.Add(new Label { Text = Loc.T("pk.mute.words.phrases.separate"), AutoSize = true, Margin = new Padding(0, 8, 0, 3) });
        mutedNewsKeywords = new FlatText { Width = 500 };
        right.Controls.Add(mutedNewsKeywords);
        right.Controls.Add(new Label { Text = Loc.T("pk.matches.title.and.summary"), AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 6) });

        right.Controls.Add(new Label { Text = L("lbl.addSite"), AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 10, 0, 4) });
        var addRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        siteBox = new FlatText { Width = 300 };
        var addSite = new FlatButton { Text = L("btn.addSite") };
        addSite.Click += (_, _) => AddFromSite();
        addRow.Controls.Add(siteBox);
        addRow.Controls.Add(addSite);
        right.Controls.Add(addRow);
        discoverStatus = new Label { AutoSize = true, Tag = "muted" };
        right.Controls.Add(discoverStatus);

        right.Controls.Add(new Label { Text = L("lbl.presets"), AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
        var presetRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        presets = new FlatComboBox { Width = 300 };
        presets.SetItems(AppConfig.PresetFeeds.Select(p => p.Name), -1);
        var addPreset = new FlatButton { Text = L("btn.addPreset") };
        addPreset.Click += (_, _) => AddPreset();
        presetRow.Controls.Add(presets);
        presetRow.Controls.Add(addPreset);
        right.Controls.Add(presetRow);

        // News of the world: continent, then country, then the open feeds of its broadcasters and newspapers (checked when the list was made, see WorldFeeds).
        right.Controls.Add(new Label { Text = L("worldfeeds.title"), AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
        right.Controls.Add(new Label { Text = L("worldfeeds.note"), AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 4) });
        var worldFeedRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        worldFeedContinent = new FlatComboBox { Width = 240 };
        worldFeedContinent.SetItems(WorldPlaces.Continents.Select(c => c.DisplayName), 0);
        worldFeedCountry = new FlatComboBox { Width = 270 };
        worldFeedRow.Controls.Add(worldFeedContinent);
        worldFeedRow.Controls.Add(worldFeedCountry);
        right.Controls.Add(worldFeedRow);
        worldFeedList = new FlatList { Width = 520, Height = 30 * 4 + 4 };
        right.Controls.Add(worldFeedList);
        var worldFeedButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var worldFeedAdd = new FlatButton { Text = L("worldfeeds.add") };
        var worldFeedAddAll = new FlatButton { Text = L("worldfeeds.addAll") };
        worldFeedButtons.Controls.Add(worldFeedAdd);
        worldFeedButtons.Controls.Add(worldFeedAddAll);
        right.Controls.Add(worldFeedButtons);
        worldFeedContinent.SelectedIndexChanged += (_, _) => FillWorldFeedCountries();
        worldFeedCountry.SelectedIndexChanged += (_, _) => FillWorldFeeds();
        worldFeedAdd.Click += (_, _) => AddWorldFeeds(onlySelected: true);
        worldFeedAddAll.Click += (_, _) => AddWorldFeeds(onlySelected: false);
        FillWorldFeedCountries();

        root.Controls.Add(left);
        root.Controls.Add(right);
        page.Controls.Add(root);

        categoryList.SelectedIndexChanged += (_, _) => ShowFeedsOfSelectedCategory();
        feedList.ItemChecked += (index, isChecked) =>
        {
            if (SelectedCategory() is { } cat && index < cat.Feeds.Count) cat.Feeds[index].Enabled = isChecked;
        };
        return page;
    }
}

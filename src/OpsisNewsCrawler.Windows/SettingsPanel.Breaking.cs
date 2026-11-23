using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 extracted Breaking settings pages. Shares the existing instance fields and theme infrastructure.</summary>
public sealed partial class SettingsPanel
{
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
        liveList.ClearRows();
        foreach (var f in workLive) liveList.AddRow($"{(string.IsNullOrWhiteSpace(f.Name) ? f.Url : f.Name)}   —   {f.Url}", f.Enabled);
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

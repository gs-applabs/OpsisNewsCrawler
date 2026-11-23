using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 extracted Weather settings pages. Shares the existing instance fields and theme infrastructure.</summary>
public sealed partial class SettingsPanel
{
    private Control BuildWeather()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("note.weather"), 700));

        var card = new FlatCard(L("tab.weather"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        citySearch = new FlatText { Width = 200 };
        var all = new FlatButton { Text = L("btn.selectAll") };
        var none = new FlatButton { Text = L("btn.selectNone") };
        var searchRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        searchRow.Controls.Add(citySearch);
        searchRow.Controls.Add(all);
        searchRow.Controls.Add(none);
        inner.Controls.Add(Row(L("lbl.search"), searchRow, 150));
        cityList = new FlatList { Width = 740, Height = 300 };
        cityList.SetShowChecks(true);
        inner.Controls.Add(cityList);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        // Any place on Earth: by name, Greek postcode or coordinates. The places are added to the list above (marked with a star).
        var geo = new FlatCard(L("geo.card"));
        var geoFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        geoFlow.Controls.Add(Note(L("geo.hint"), 700));
        geoQuery = new FlatText { Width = 380 };
        geoSearchButton = new FlatButton { Text = L("geo.search") };
        geoSearchButton.SetPrimary(true);
        var geoRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        geoRow.Controls.Add(geoQuery);
        geoRow.Controls.Add(geoSearchButton);
        geoFlow.Controls.Add(geoRow);
        geoList = new FlatList { Width = 740, Height = 30 * 5 + 4 };
        geoList.SetShowChecks(true);
        geoFlow.Controls.Add(geoList);
        geoAddButton = new FlatButton { Text = L("geo.add") };
        geoCleanupButton = new FlatButton { Text = L("geo.cleanup") };
        geoMyLocationButton = new FlatButton { Text = L("geo.myloc") };
        var geoButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        geoButtons.Controls.Add(geoAddButton);
        geoButtons.Controls.Add(geoCleanupButton);
        geoButtons.Controls.Add(geoMyLocationButton);
        geoFlow.Controls.Add(geoButtons);
        geoStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(700, 0), Margin = new Padding(0, 6, 0, 0), UseMnemonic = false };
        geoFlow.Controls.Add(geoStatus);
        geoFlow.Controls.Add(Note(L("geo.note"), 700));
        geo.Controls.Add(geoFlow);
        flow.Controls.Add(geo);
        geoSearchButton.Click += async (_, _) => await RunGeoSearchAsync();
        geoAddButton.Click += async (_, _) => await AddGeoPlacesAsync();
        geoCleanupButton.Click += (_, _) => CleanupGeoPlaces();
        geoMyLocationButton.Click += async (_, _) => await AddMyLocationAsync();

        weatherMapOnClick = new FlatToggle { RowWidth = 600, Text = Loc.T("pk.click.a.city.on") };
        flow.Controls.Add(weatherMapOnClick);
        flow.Controls.Add(Note(Loc.T("pk.the.map.openstreetmap.needs"), 640));

        // What the weather line shows besides the temperature.
        var fields = new FlatCard(Loc.T("pk.details.shown"));
        var fieldsFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        weatherFieldChecks.Clear();
        foreach (var id in WeatherInfo.FieldIds)
        {
            var check = new FlatCheckBox { Text = Loc.T("weather." + (id == "feels" ? "feels" : id)) };
            weatherFieldChecks[id] = check;
            fieldsFlow.Controls.Add(check);
        }
        fieldsFlow.Controls.Add(Note(Loc.T("pk.the.temperature.is.always"), 600));
        fields.Controls.Add(fieldsFlow);
        flow.Controls.Add(fields);

        // The animated icon can show the current weather of the first city.
        weatherInIcon = new FlatToggle { RowWidth = 600, Text = L("hazard.weatherIcon") };
        flow.Controls.Add(weatherInIcon);

        citySearch.TextValueChanged += (_, _) => ShowCities();
        cityList.ItemChecked += (index, isChecked) =>
        {
            if (index < 0 || index >= shownCities.Count) return;
            var id = shownCities[index].Id;
            if (isChecked) { if (!workCities.Contains(id)) workCities.Add(id); }
            else workCities.Remove(id);
        };
        all.Click += (_, _) => { foreach (var c in shownCities) if (!workCities.Contains(c.Id)) workCities.Add(c.Id); ShowCities(); };
        none.Click += (_, _) => { foreach (var c in shownCities) workCities.Remove(c.Id); ShowCities(); };
        ShowCities();
        flow.Controls.Add(BuildWorldBrowser());
        return page;
    }

    /// <summary>Continent, then country, then its main cities with a check box each; a ticked city joins the weather list (Europe first, Greece has its own list above).</summary>

    private Control BuildWorldBrowser()
    {
        var card = new FlatCard(L("world.card"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        inner.Controls.Add(Note(L("world.note"), 700));
        worldContinent = new FlatComboBox { Width = 380 };
        worldContinent.SetItems(WorldPlaces.Continents.Select(c => c.DisplayName), 0);
        inner.Controls.Add(Row(L("world.continent"), worldContinent, 260));
        worldCountry = new FlatComboBox { Width = 380 };
        inner.Controls.Add(Row(L("world.country"), worldCountry, 260));
        worldList = new FlatList { Width = 740, Height = 30 * 6 + 4 };
        worldList.SetShowChecks(true);
        inner.Controls.Add(worldList);
        card.Controls.Add(inner);

        worldContinent.SelectedIndexChanged += (_, _) => FillWorldCountries();
        worldCountry.SelectedIndexChanged += (_, _) => FillWorldCities();
        worldList.ItemChecked += (index, isChecked) => WorldCityChecked(index, isChecked);
        FillWorldCountries();
        return card;
    }

    private void FillWorldCountries()
    {
        var continent = WorldPlaces.Continents.ElementAtOrDefault(Math.Max(0, worldContinent.SelectedIndex));
        var names = continent is null ? new List<string>() : continent.Countries.OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(c => c.DisplayName).ToList();
        worldCountry.SetItems(names, 0);
        FillWorldCities();
    }

    private void FillWorldCities()
    {
        var continent = WorldPlaces.Continents.ElementAtOrDefault(Math.Max(0, worldContinent.SelectedIndex));
        shownCountry = continent?.Countries.OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase).ElementAtOrDefault(Math.Max(0, worldCountry.SelectedIndex));
        worldList.ClearRows();
        if (shownCountry is null) return;
        foreach (var city in shownCountry.Cities) worldList.AddRow(Loc.Pick(city.NameEl, city.NameEn), workCities.Contains(city.Id));
    }

    private void WorldCityChecked(int index, bool isChecked)
    {
        if (refreshingWorld || shownCountry is null || index < 0 || index >= shownCountry.Cities.Count) return;
        var city = shownCountry.Cities[index];
        if (isChecked)
        {
            if (workCustom.All(c => c.Id != city.Id))
            {
                if (workCustom.Count >= GreekCities.MaxCustom) { worldList.SetChecked(index, false); return; }
                workCustom.Add(city);
            }
            if (!workCities.Contains(city.Id)) workCities.Add(city.Id);
        }
        else workCities.Remove(city.Id);
        var keep = AlertCityIdSelected();
        GreekCities.SetCustom(workCustom);
        ShowCities();
        FillAlertCities(keep);
        MarkDirty();
    }

    /// <summary>The natural-disasters flow: its own page in the Sources group (where from, how far, how strong, how old, and the animated icon).</summary>

    private Control BuildHazards()
    {
        var page = NewPage(out var flow);
        // Natural disasters: where from, how far, how strong, how old; and whether the animated icon shows them.
        var hazards = new FlatCard(L("hazard.card"));
        var hazardFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        hazardFlow.Controls.Add(Note(L("hazard.note"), 700));
        hazardRegion = new FlatComboBox { Width = 300 };
        hazardRegion.SetItems(new[] { L("hazard.region.greece"), L("hazard.region.europe"), L("hazard.region.world") }, 0);
        hazardFlow.Controls.Add(Row(L("hazard.region"), hazardRegion, 260));
        hazardMinMagnitude = new FlatComboBox { Width = 120 };
        hazardMinMagnitude.SetItems(MagnitudeChoices.Select(m => m.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture)), 3);
        hazardFlow.Controls.Add(Row(L("hazard.minmag"), hazardMinMagnitude, 260));
        hazardMaxAge = new FlatNumber { Width = 120 };
        hazardMaxAge.Configure(1, 24 * 14, 6);
        hazardFlow.Controls.Add(Row(L("hazard.maxage"), hazardMaxAge, 260));
        hazardRefresh = new FlatNumber { Width = 120 };
        hazardRefresh.Configure(5, 240, 5);
        hazardFlow.Controls.Add(Row(L("hazard.refresh"), hazardRefresh, 260));
        var kindsRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(470, 0) };
        hazardKindChecks.Clear();
        foreach (var kind in Enum.GetValues<HazardKind>())
        {
            var check = new FlatCheckBox { Text = HazardText.KindName(kind), Margin = new Padding(0, 4, 16, 4) };
            hazardKindChecks[kind] = check;
            kindsRow.Controls.Add(check);
        }
        hazardFlow.Controls.Add(Row(L("hazard.kinds"), kindsRow, 240));
        var sourcesRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(470, 0) };
        hazardSourceChecks.Clear();
        foreach (var (source, name) in new[] { (HazardSources.Usgs, "USGS"), (HazardSources.Emsc, "EMSC"), (HazardSources.Gdacs, "GDACS"), (HazardSources.Eonet, "NASA EONET") })
        {
            var check = new FlatCheckBox { Text = name, Margin = new Padding(0, 4, 16, 4) };
            hazardSourceChecks[source] = check;
            sourcesRow.Controls.Add(check);
        }
        hazardFlow.Controls.Add(Row(L("hazard.src"), sourcesRow, 240));
        hazardInIcon = new FlatToggle { RowWidth = 600, Text = L("hazard.icon") };
        hazardFlow.Controls.Add(hazardInIcon);
        hazards.Controls.Add(hazardFlow);
        flow.Controls.Add(hazards);
        return page;
    }

    private void ShowCities()
    {
        if (cityList is null || cityList.IsDisposed) return;
        var term = citySearch.Value.Trim();
        shownCities = GreekCities.All.Where(c => term.Length == 0 || c.NameEl.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                                 c.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        cityList.ClearRows();
        foreach (var c in shownCities) cityList.AddRow(CityName(c) + (GreekCities.Custom.Contains(c) ? "  \u2605" : ""), workCities.Contains(c.Id));
        RefreshWorldChecks();
    }

    /// <summary>The check marks of the world list follow the weather list (a city can be removed there).</summary>
    private void RefreshWorldChecks()
    {
        if (worldList is null || shownCountry is null) return;
        refreshingWorld = true;
        try { for (var i = 0; i < shownCountry.Cities.Count; i++) worldList.SetChecked(i, workCities.Contains(shownCountry.Cities[i].Id)); }
        finally { refreshingWorld = false; }
    }

    // ---- places found by name, postcode or coordinates -------------------------------------------

    private async Task RunGeoSearchAsync()
    {
        var query = geoQuery.Value.Trim();
        if (query.Length < 2) return;
        geoSearchButton.Enabled = false;
        geoStatus.Text = Loc.T("pk.testing");
        try
        {
            geoHits = (await GeoSearch.SearchAsync(query, Loc.IsGreek ? "el" : "en", Loc.IsGreek, CancellationToken.None)).ToList();
            geoList.ClearRows();
            foreach (var h in geoHits) geoList.AddRow(h.Label, false);
            geoStatus.Text = geoHits.Count == 0 ? L("geo.none") : Loc.T("geo.found", geoHits.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            geoStatus.Text = Loc.T("geo.error", ex.Message);
        }
        finally
        {
            if (!IsDisposed) geoSearchButton.Enabled = true;
        }
    }

    private async Task AddGeoPlacesAsync()
    {
        var chosen = Enumerable.Range(0, geoHits.Count).Where(i => geoList.GetChecked(i)).Select(i => geoHits[i]).ToList();
        if (chosen.Count == 0) return;
        geoAddButton.Enabled = false;
        var added = 0;
        try
        {
            foreach (var place in chosen)
            {
                if (workCustom.Count >= GreekCities.MaxCustom) break;
                // The name in the other language of the program, when the place has one (best effort; the name found is used otherwise).
                var other = place.OpenMeteoId is { } id ? await GeoSearch.NameInAsync(id, Loc.IsGreek ? "en" : "el", CancellationToken.None) : null;
                var city = Loc.IsGreek ? GeoSearch.ToCity(place, place.Name, other) : GeoSearch.ToCity(place, other, place.Name);
                if (workCustom.All(c => c.Id != city.Id)) workCustom.Add(city);
                if (!workCities.Contains(city.Id)) workCities.Add(city.Id);
                added++;
            }
            var keep = AlertCityIdSelected();
            GreekCities.SetCustom(workCustom);
            ShowCities();
            FillAlertCities(keep);
            geoStatus.Text = Loc.T("geo.added", added);
        }
        finally
        {
            if (!IsDisposed) geoAddButton.Enabled = true;
        }
    }

    /// <summary>Forgets the added places that are not ticked in the list.</summary>
    /// <summary>Opt-in: asks the Windows Location service once (after a confirmation) and adds the rounded point as a place of the weather list.</summary>
    private async Task AddMyLocationAsync()
    {
        if (MessageBox.Show(FindForm(), L("geo.myloc.consent"), L("geo.myloc"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        geoMyLocationButton.Enabled = false;
        try
        {
            var here = await DeviceLocation.GetApproximateAsync();
            if (here is null) { geoStatus.Text = L("geo.myloc.denied"); return; }
            if (workCustom.Count >= GreekCities.MaxCustom) return;
            var name = L("geo.myloc.name");
            var city = GeoSearch.ToCity(new GeoPlace(name, null, null, null, "", here.Value.Lat, here.Value.Lon), name, name);
            if (workCustom.All(c => c.Id != city.Id)) workCustom.Add(city);
            if (!workCities.Contains(city.Id)) workCities.Add(city.Id);
            var keep = AlertCityIdSelected();
            GreekCities.SetCustom(workCustom);
            ShowCities();
            FillAlertCities(keep);
            geoStatus.Text = Loc.T("geo.added", 1);
        }
        finally { if (!IsDisposed) geoMyLocationButton.Enabled = true; }
    }

    private void CleanupGeoPlaces()
    {
        var keep = AlertCityIdSelected();
        workCustom = workCustom.Where(c => workCities.Contains(c.Id)).ToList();
        GreekCities.SetCustom(workCustom);
        workCities = workCities.Where(id => GreekCities.Find(id) is not null).ToList();
        ShowCities();
        FillAlertCities(keep);
    }

    private string? AlertCityIdSelected() => alertCity is null || alertCity.SelectedIndex <= 0 || alertCity.SelectedIndex > GreekCities.All.Count ? null : GreekCities.All[alertCity.SelectedIndex - 1].Id;

    /// <summary>The "my area" list of the severe-weather card: automatic, then every city (built-in and added).</summary>
    private void FillAlertCities(string? keepId)
    {
        var index = keepId is null ? 0 : Math.Max(0, GreekCities.All.ToList().FindIndex(x => x.Id == keepId) + 1);
        alertCity.SetItems(new[] { Loc.T("pk.automatic.the.first.city") }.Concat(GreekCities.All.Select(CityName)), index);
    }

    // ---- updates -----------------------------------------------------------------------------
}

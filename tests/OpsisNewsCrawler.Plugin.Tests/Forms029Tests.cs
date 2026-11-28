using System.Windows.Forms;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>Smoke test: the new 0.29.0 windows can be built and driven without a desktop session (not a visual check).</summary>
public sealed class Forms029Tests
{
    private static void OnSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null) throw new Xunit.Sdk.XunitException(error.ToString());
    }

    [Fact]
    public void FocusReaderAndSourceComparisonBuildForNormalAndEmptyInput() => OnSta(() =>
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            new NewsItem("First headline of the story", "https://a.example/1", "A", now, new string('x', 3000)),
            new NewsItem("Second headline of the story", "https://b.example/2", "B", now.AddHours(-1))
        };
        using (var reader = new FocusReaderForm(items, 99)) { reader.CreateControl(); Assert.True(reader.Controls.Count >= 4); }
        using (var empty = new FocusReaderForm(Array.Empty<NewsItem>())) empty.CreateControl();
        var cluster = new StoryCluster(items[0].Url, items[0].Title, now, items, 2);
        using var compare = new P3EvidenceForm(cluster);
        compare.CreateControl();
        Assert.True(compare.Controls.Count >= 3);
    });
    [Fact]
    public void YahooSearchCancelsOnQueryChangeAndDiscardsOldResult() => OnSta(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "crawler-yahoo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var originalEnabled = YahooAccess.Enabled;
        try
        {
            var cfg = Path.Combine(dir, "config.json");
            new AppConfig { HistoryFromYahoo = false, BarsEnabled = false, CheckForUpdates = false }.Save(cfg);
            using var host = new TickerHost(cfg);
            using var panel = new SettingsPanel(host, false);
            using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-4000, 0), ClientSize = new System.Drawing.Size(1100, 700) };
            form.Controls.Add(panel);
            form.Show();
            panel.SelectPage("markets");
            Application.DoEvents();
            object Field(string name) => typeof(SettingsPanel).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(panel)!;
            var toggle = Field("yahooHistory");
            toggle.GetType().GetProperty("Checked")!.SetValue(toggle, true);
            var box = Field("yahooSearch");
            var value = box.GetType().GetProperty("Value")!;
            value.SetValue(box, "gold");
            var complete = new TaskCompletionSource<IReadOnlyList<YahooHit>>();
            CancellationToken observed = default;
            var pending = panel.RunYahooSearchAsync((_, token) => { observed = token; return complete.Task; });
            Assert.True(observed.CanBeCanceled);
            Assert.False(YahooAccess.Enabled); // manual request must not enable global background work
            value.SetValue(box, "silver");
            Assert.True(observed.IsCancellationRequested);
            var status = (Label)Field("yahooStatus");
            var expected = status.Text;
            complete.SetResult(new[] { new YahooHit("OLD", "obsolete", "EQUITY", "test") });
            for (int i = 0; i < 50 && !pending.IsCompleted; i++) { Application.DoEvents(); Thread.Sleep(10); }
            Assert.True(pending.IsCompletedSuccessfully);
            Assert.Equal(expected, status.Text);
            Assert.DoesNotContain((List<YahooHit>)Field("yahooHits"), h => h.Symbol == "OLD");
            form.Close();
        }
        finally { YahooAccess.Enabled = originalEnabled; try { Directory.Delete(dir, true); } catch (IOException) { } }
    });

}

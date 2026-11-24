using System.Diagnostics;
using System.Runtime.InteropServices;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

[CollectionDefinition("p0-gdi-soak", DisableParallelization = true)]
public sealed class P0GdiSoakCollection { }

[Collection("p0-gdi-soak")]
public sealed class P0GdiSoakTests
{
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr hProcess, int flags);

    [Fact]
    public void RepeatedTickerCanvasPaintAndDisposeDoesNotAccumulateGdiHandles()
    {
        Exception? error = null;
        var ui = new Thread(() =>
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                // Warm up the WinForms/GDI+ static caches before measuring steady-state use.
                using (var warm = new TickerCanvas { Size = new Size(850, 48) })
                {
                    warm.SetItems(new[] { new NewsItem("Warmup", "https://example.org", "Test", DateTimeOffset.UtcNow) });
                    using var image = warm.RenderFrame();
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var before = GetGuiResources(process.Handle, 0);
                for (var round = 0; round < 20; round++)
                {
                    using var canvas = new TickerCanvas { Size = new Size(850, 48) };
                    canvas.SetItems(new[]
                    {
                        new NewsItem("Test headline", "https://example.org/story", "Test", DateTimeOffset.UtcNow),
                        new NewsItem("Second headline", "https://example.org/second", "Another", DateTimeOffset.UtcNow)
                    });
                    for (var frame = 0; frame < 12; frame++)
                    {
                        canvas.SetOffset(frame * 18);
                        using var image = canvas.RenderFrame();
                        Assert.Equal(850, image.Width);
                    }
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var after = GetGuiResources(process.Handle, 0);
                Assert.InRange(after - before, -50, 35); // warm caches + nonparallel collection avoid process-global interference
            }
            catch (Exception ex) { error = ex; }
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        Assert.True(ui.Join(TimeSpan.FromSeconds(90)));
        if (error is not null) throw new Exception("GDI soak failed on STA thread", error);
    }
}

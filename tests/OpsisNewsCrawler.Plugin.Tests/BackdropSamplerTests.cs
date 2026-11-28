using System.Runtime.InteropServices;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public class BackdropSamplerTests
{
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();

    [Fact]
    public void TheSamplerMeasuresALightAndADarkAreaOfTheScreen()
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                // Both regions are painted simultaneously in ONE HWND. The old test replaced
                // a white HWND with a black HWND and could capture the previous DWM frame.
                var screen = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
                using var form = new Form { FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(screen.Left + 40, screen.Top + 40),
                    ClientSize = new Size(240, 80), BackColor = Color.White, TopMost = true,
                    ShowInTaskbar = false, AutoScaleMode = AutoScaleMode.None };
                using var dark = new Panel { BackColor = Color.Black, Bounds = new Rectangle(120, 0, 120, 80) };
                form.Controls.Add(dark);
                form.Show();
                Native.KeepTopMost(form.Handle);
                form.Refresh();
                var lightArea = form.RectangleToScreen(new Rectangle(10, 10, 100, 60));
                var darkArea = form.RectangleToScreen(new Rectangle(130, 10, 100, 60));
                double? light = null, black = null;
                // Wait for the real compositor frame rather than weakening contrast assertions.
                for (int i = 0; i < 50; i++)
                {
                    Application.DoEvents();
                    form.Update();
                    DwmFlush();
                    light = BackdropSampler.Sample(lightArea);
                    black = BackdropSampler.Sample(darkArea);
                    if (light > .9 && black < .1) break;
                    Thread.Sleep(40);
                }
                Assert.True(light > .9 && black < .1,
                    $"Expected simultaneously visible white and black patches; white={light}, black={black}");
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(60000));
        if (error is not null) throw new Exception("Screen contrast regression", error);
    }
}

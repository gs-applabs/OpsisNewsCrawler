using System.Runtime.InteropServices;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

// Real Win32 HWNDs, with a disposable shell surrogate rather than mutating Explorer.
public sealed class TaskbarWindowTests
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    [Fact]
    public void RestoreAboveShellPreservesUnrelatedTopmostWindowAndFocus()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var bounds = new Rectangle(40, 40, 240, 60);
                Form Make() => new() { StartPosition = FormStartPosition.Manual, Bounds = bounds,
                    FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TopMost = true };
                using var ticker = Make();
                using var shell = Make();
                using var popup = Make();
                ticker.Show(); Native.KeepTopMost(ticker.Handle);
                shell.Show(); Native.KeepTopMost(shell.Handle);
                popup.Show(); Native.KeepTopMost(popup.Handle);
                Application.DoEvents();
                Assert.True(Native.IsWindowCovering(ticker.Handle, shell.Handle));
                Assert.True(Native.IsWindowCovering(ticker.Handle, popup.Handle));
                var foreground = GetForegroundWindow();
                Assert.True(Native.RestoreAboveWindow(ticker.Handle, shell.Handle));
                Assert.False(Native.IsWindowCovering(ticker.Handle, shell.Handle));
                Assert.True(Native.IsWindowCovering(ticker.Handle, popup.Handle));
                Assert.Equal(foreground, GetForegroundWindow());
                Assert.False(Native.RestoreAboveWindow(ticker.Handle, shell.Handle));
                // Repeat a real z-order transition, not just a mocked policy.
                for (int i = 0; i < 20; i++)
                {
                    Native.KeepTopMost(shell.Handle);
                    Native.KeepTopMost(popup.Handle);
                    Assert.True(Native.RestoreAboveWindow(ticker.Handle, shell.Handle));
                    Assert.False(Native.IsWindowCovering(ticker.Handle, shell.Handle));
                    Assert.True(Native.IsWindowCovering(ticker.Handle, popup.Handle));
                }
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(60000), "Win32 test timed out");
        if (error is not null) throw new Exception("Win32 z-order regression", error);
    }
}

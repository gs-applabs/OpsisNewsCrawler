using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>One owner of map-window chrome, WebView security, tile identity and disposal.
/// Derived forms own data markup and explicit external-link policy, not another browser lifecycle.</summary>
internal abstract class MapDetailForm : Form
{
    private const int ToolbarHeight = 44;
    private const string TileReferer = "https://github.com/gs-applabs/OpsisNewsCrawler";
    private readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(30, 30, 30) };
    private readonly Panel toolbar = new() { Dock = DockStyle.Bottom, Height = ToolbarHeight };
    private readonly FlatButton maximize = new(), close = new();
    private readonly Label title = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label message = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
    private readonly Font bodyFont = new("Segoe UI", 9.5f);
    private readonly Font buttonFont = new("Segoe UI Symbol", 11f);
    private readonly string html;
    private readonly Func<Exception,string> errorText;
    private Rectangle normalBounds;
    private bool maximized, closing, released;

    protected MapDetailForm(string html, string windowTitle, string toolbarTitle, Rectangle anchorBar,
        int width, int contentHeight, Func<Exception,string> errorText)
    {
        this.html = html;
        this.errorText = errorText;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Text = windowTitle;
        Icon = AppIcon.Get();
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = bodyFont;
        KeyPreview = true;
        var height = contentHeight + ToolbarHeight;
        var screen = Screen.FromRectangle(anchorBar).Bounds;
        var x = screen.Right - width;
        var y = anchorBar.Top - height;
        if (y < screen.Top) y = Math.Min(screen.Bottom - height, anchorBar.Bottom);
        normalBounds = new Rectangle(x, Math.Max(screen.Top, y), width, height);
        Bounds = normalBounds;
        maximize.Text = "⤢";
        close.Text = "✕";
        foreach (var b in new[] { maximize, close })
        {
            b.Padding = new Padding(10, 3, 10, 3);
            b.Font = buttonFont;
            b.Margin = new Padding(0, 6, 6, 6);
        }
        maximize.Click += (_, _) => ToggleMaximize();
        close.Click += (_, _) => Close();
        title.Text = toolbarTitle;
        title.ForeColor = Theme.TextMuted;
        title.BackColor = Theme.Surface;
        title.Dock = DockStyle.Fill;
        title.Padding = new Padding(14, 0, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 0, 8, 0), BackColor = Theme.Surface };
        buttons.Controls.AddRange(new Control[] { maximize, close });
        toolbar.BackColor = Theme.Surface;
        toolbar.Controls.Add(buttons);
        toolbar.Controls.Add(title);
        message.ForeColor = Theme.TextMuted;
        Controls.Add(message);
        Controls.Add(web);
        Controls.Add(toolbar);
        web.BringToFront();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            if (maximized) ToggleMaximize(); else Close();
        };
        Load += async (_, _) => await StartAsync();
    }

    internal virtual bool AllowsExternalLink(string? url) => false;

    private void OpenLink(string? url)
    {
        if (closing || url is null || !AllowsExternalLink(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private async Task StartAsync()
    {
        try
        {
            if (closing || IsDisposed) return;
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpsisNewsCrawler", "webview2");
            var env = await CoreWebView2Environment.CreateAsync(null, folder);
            if (closing || IsDisposed || web.IsDisposed) return;
            await web.EnsureCoreWebView2Async(env);
            if (closing || IsDisposed || web.IsDisposed) return;
            var core = web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.NewWindowRequested += (_, e) => { e.Handled = true; OpenLink(e.Uri); };
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NavigationStarting += (_, e) =>
            {
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) && target.Scheme is "data" or "about") return;
                e.Cancel = true;
                OpenLink(e.Uri);
            };
            core.AddWebResourceRequestedFilter("https://tile.openstreetmap.org/*", CoreWebView2WebResourceContext.Image);
            core.WebResourceRequested += (_, e) =>
            {
                try { e.Request.Headers.SetHeader("Referer", TileReferer); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { }
            };
            core.NavigateToString(html);
        }
        catch (Exception ex)
        {
            if (closing || IsDisposed || web.IsDisposed) return;
            web.Visible = false;
            message.Text = errorText(ex);
            message.Visible = true;
            message.BringToFront();
        }
    }

    internal async Task<string?> SaveSnapshotAsync(string png, int waitMs)
    {
        for (var i = 0; i < 100 && !closing && web.CoreWebView2 is null; i++) await Task.Delay(100);
        if (closing || IsDisposed) return "map closed";
        if (web.CoreWebView2 is null) return message.Visible ? message.Text : "web view not created";
        await Task.Delay(waitMs);
        if (closing || IsDisposed || web.CoreWebView2 is null) return "map closed";
        await using var stream = File.Create(png);
        await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        return null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (maximized) return;
        using var pen = new Pen(Theme.Accent, 3f);
        e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
    }

    private void ToggleMaximize()
    {
        if (!maximized)
        {
            normalBounds = Bounds;
            maximized = true;
            Bounds = Screen.FromControl(this).Bounds;
            maximize.Text = "⤡";
        }
        else
        {
            maximized = false;
            Bounds = normalBounds;
            maximize.Text = "⤢";
        }
        Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: not in Alt+Tab
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.MarkNoSnap(Handle);
    }


    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        closing = true;
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        var release = disposing && !released;
        if (release)
        {
            closing = true;
            released = true;
            web.Dispose();
        }
        try { base.Dispose(disposing); }
        finally
        {
            if (release) { buttonFont.Dispose(); bodyFont.Dispose(); }
        }
    }
}

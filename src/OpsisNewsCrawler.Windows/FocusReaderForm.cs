using System.ComponentModel;
using System.Diagnostics;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// N17: a calm reading view of the Crawler: one headline and its feed summary in large type, no panels around it. Arrows (or PageUp/PageDown) move
/// between stories, Enter opens the article, Esc closes. It shows only what the feed delivered; it is not a workspace and does not copy full articles.
/// </summary>
public sealed class FocusReaderForm : Form
{
    private readonly IReadOnlyList<NewsItem> items;
    private int index;
    private readonly Label meta = new() { Dock = DockStyle.Top, Height = 34, AutoEllipsis = true };
    private readonly Label title = new() { Dock = DockStyle.Top, Height = 150, AutoEllipsis = true, UseMnemonic = false };
    private readonly Label body = new() { Dock = DockStyle.Fill, UseMnemonic = false };
    private readonly Label hint = new() { Dock = DockStyle.Bottom, Height = 30, AutoEllipsis = true };

    public FocusReaderForm(IReadOnlyList<NewsItem> items, int start = 0)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        index = items.Count == 0 ? 0 : Math.Clamp(start, 0, items.Count - 1);
        Text = Loc.IsGreek ? "Λειτουργία εστίασης" : "Focus mode";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(860, 600);
        MinimumSize = new Size(480, 360);
        KeyPreview = true;
        Padding = new Padding(36, 24, 36, 16);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 11f);
        title.Font = new Font("Segoe UI Semibold", 20f);
        body.Font = new Font("Segoe UI", 14f);
        body.AccessibleRole = AccessibleRole.Text;
        title.AccessibleRole = AccessibleRole.TitleBar;
        hint.Text = Loc.IsGreek ? "← →: προηγούμενο/επόμενο · Enter: άνοιγμα άρθρου · Esc: κλείσιμο" : "← →: previous/next · Enter: open article · Esc: close";
        Controls.Add(body); Controls.Add(title); Controls.Add(meta); Controls.Add(hint);
        KeyDown += OnKey;
        Theme.Changed += ApplyLook;
        FormClosed += (_, _) => Theme.Changed -= ApplyLook;
        ApplyLook();
        ShowCurrent();
    }

    private void ApplyLook()
    {
        if (IsDisposed) return;
        // High contrast: the system colours, as everywhere else the program follows them.
        var high = SystemInformation.HighContrast;
        BackColor = high ? SystemColors.Window : Theme.Background;
        ForeColor = high ? SystemColors.WindowText : Theme.Text;
        meta.ForeColor = hint.ForeColor = high ? SystemColors.WindowText : Theme.TextMuted;
    }

    private void ShowCurrent()
    {
        if (items.Count == 0)
        {
            title.Text = Loc.IsGreek ? "Δεν υπάρχουν ειδήσεις." : "No stories.";
            meta.Text = body.Text = "";
            return;
        }
        var item = items[index];
        meta.Text = $"{index + 1}/{items.Count}  ·  {item.Source}  ·  {item.PublishedAt.ToLocalTime():dd/MM HH:mm}";
        title.Text = item.Title;
        var summary = item.Summary ?? "";
        if (summary.Length > 1500) summary = summary[..1500] + "…";
        body.Text = summary.Length == 0 ? (Loc.IsGreek ? "Η πηγή δεν έδωσε περίληψη." : "The source gave no summary.") : summary;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape: Close(); break;
            case Keys.Right or Keys.PageDown or Keys.Space when index < items.Count - 1: index++; ShowCurrent(); break;
            case Keys.Left or Keys.PageUp when index > 0: index--; ShowCurrent(); break;
            case Keys.Enter: Open(); break;
            default: return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void Open()
    {
        if (items.Count == 0 || !NetGuard.IsPublicHttpUrl(items[index].Url)) return;
        try { Process.Start(new ProcessStartInfo(items[index].Url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
    }
}

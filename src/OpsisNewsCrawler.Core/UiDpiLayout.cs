namespace OpsisNewsCrawler.Core;

/// <summary>Pure DPI coordinate policy for owner-drawn UI and live video popup bounds.</summary>
public static class UiDpiLayout
{
    public static int Pixels(int logicalPx, int dpi) =>
        Math.Max(1, (int)Math.Round(logicalPx * Math.Clamp(dpi, 96, 480) / 96d));

    public static System.Drawing.Rectangle PopupAt(System.Drawing.Rectangle anchor,
        System.Drawing.Rectangle workArea, int dpi, int logicalWidth = 520, int toolbarPx = 44)
    {
        var width = Math.Min(Pixels(logicalWidth, dpi), Math.Max(1, workArea.Width - 16));
        var toolbar = Pixels(toolbarPx, dpi);
        var height = Math.Min(workArea.Height, Math.Max(1, (int)Math.Round(width * 9.0 / 16) + toolbar));
        var x = Math.Clamp(anchor.Right - width - 16, workArea.Left, workArea.Right - width);
        var y = Math.Clamp(anchor.Top - height - 10, workArea.Top, workArea.Bottom - height);
        return new System.Drawing.Rectangle(x, y, width, height);
    }
}

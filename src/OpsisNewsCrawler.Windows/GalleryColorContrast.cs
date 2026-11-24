using System.Drawing;

namespace OpsisNewsCrawler.Windows;

/// <summary>WCAG relative luminance contrast for an accent-backed alert banner.
/// No extra palette or OS accent engine: Opsis/Theme owns every color.</summary>
public static class GalleryColorContrast
{
    public static int CardHeightForDpi(int dpi) =>
        Math.Clamp((int)Math.Round(100 * Math.Clamp(dpi, 96, 384) / 96.0), 100, 400);

    public static int CardHeightForDpi(int dpi, int fontScale) =>
        (int)Math.Ceiling(CardHeightForDpi(dpi) * Math.Max(100, Math.Clamp(fontScale, 80, 160)) / 100.0);

    public static Color Readable(Color preferred, Color background) =>
        ContrastRatio(preferred, background) >= 4.5 ? preferred : Foreground(background);

    public static double ContrastRatio(Color foreground, Color background)
    {
        static double Lum(Color color)
        {
            double L(byte v)
            {
                var n = v / 255.0;
                return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * L(color.R) + 0.7152 * L(color.G) + 0.0722 * L(color.B);
        }
        var a = Lum(foreground);
        var b = Lum(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static Color Foreground(Color accent)
    {
        var rgb = new[] { accent.R, accent.G, accent.B }
            .Select(v => v / 255d).Select(v => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4))
            .ToArray();
        var luminance = 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
        var whiteContrast = 1.05 / (luminance + 0.05);
        var blackContrast = (luminance + 0.05) / 0.05;
        return whiteContrast >= blackContrast ? Color.White : Color.Black;
    }
}

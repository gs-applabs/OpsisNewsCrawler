using System.Drawing;

namespace OpsisNewsCrawler.Windows;

/// <summary>WCAG relative luminance contrast for an accent-backed alert banner.
/// No extra palette or OS accent engine: Opsis/Theme owns every color.</summary>
public static class GalleryColorContrast
{
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

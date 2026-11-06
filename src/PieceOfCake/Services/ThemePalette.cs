using System.Windows;
using System.Windows.Media;

namespace PieceOfCake.Services;

/// <summary>Writes the colours of the dark or light Windows 11 flyout look into the application resources.</summary>
internal static class ThemePalette
{
    /// <summary>Tint laid over the system acrylic, measured against the real quick settings panel.</summary>
    public static Color AcrylicTint(bool light) =>
        light ? Color.FromArgb(0xCC, 0xFC, 0xFC, 0xFC) : Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E);

    /// <summary>Opaque background used when transparency effects are off in Windows.</summary>
    public static Color SolidBackground(bool light) =>
        light ? Color.FromRgb(0xF2, 0xF2, 0xF2) : Color.FromRgb(0x24, 0x24, 0x24);

    public static void Apply(ResourceDictionary resources, bool light, Color accent)
    {
        Set(resources, "AccentBrush", accent);
        Set(resources, "OnAccentBrush", light ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x00, 0x00, 0x00));
        Set(resources, "TextBrush", light ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        Set(resources, "SubTextBrush", light ? Color.FromRgb(0x5F, 0x5F, 0x5F) : Color.FromRgb(0xB4, 0xB4, 0xB4));
        Set(resources, "OverlayBrush", light ? Color.FromRgb(0x00, 0x00, 0x00) : Color.FromRgb(0xFF, 0xFF, 0xFF));

        Set(resources, "TileBrush", light ? Color.FromArgb(0x0F, 0, 0, 0) : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
        Set(resources, "TileHoverBrush", light ? Color.FromArgb(0x1A, 0, 0, 0) : Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        Set(resources, "TilePressedBrush", light ? Color.FromArgb(0x0A, 0, 0, 0) : Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
        Set(resources, "TrackBrush", light ? Color.FromArgb(0x33, 0, 0, 0) : Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        Set(resources, "TileOffBrush", light ? Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
        Set(resources, "TileOffBorderBrush", light ? Color.FromArgb(0x0F, 0, 0, 0) : Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
        Set(resources, "DividerBrush", light ? Color.FromArgb(0x10, 0, 0, 0) : Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
        Set(resources, "FooterBrush", light ? Color.FromArgb(0x06, 0, 0, 0) : Color.FromArgb(0x38, 0, 0, 0));

        Set(resources, "SliderTrackBrush", light ? Color.FromArgb(0x72, 0, 0, 0) : Color.FromArgb(0x8D, 0xFF, 0xFF, 0xFF));
        Set(resources, "SliderThumbRingBrush", light ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x45, 0x45, 0x45));
    }

    private static void Set(ResourceDictionary resources, string key, Color color)
    {
        if (resources[key] is not SolidColorBrush current || current.Color != color)
        {
            // Styles freeze the brushes they use, so a new brush replaces the old one (all uses are DynamicResource).
            resources[key] = new SolidColorBrush(color);
        }
    }
}

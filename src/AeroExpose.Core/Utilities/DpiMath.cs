using AeroExpose.Core.Models;

namespace AeroExpose.Core.Utilities;

public static class DpiMath
{
    public const double DefaultDpi = 96d;

    public static double PixelsToDips(double pixels, uint dpi) => pixels * DefaultDpi / SafeDpi(dpi);

    public static double DipsToPixels(double dips, uint dpi) => dips * SafeDpi(dpi) / DefaultDpi;

    public static LayoutRect PixelsToDips(LayoutRect rect, uint dpi) => new(
        PixelsToDips(rect.X, dpi),
        PixelsToDips(rect.Y, dpi),
        PixelsToDips(rect.Width, dpi),
        PixelsToDips(rect.Height, dpi));

    public static LayoutRect DesktopPixelsToOverlayPixels(PixelRect desktopRect, PixelRect monitorBounds) => new(
        desktopRect.Left - monitorBounds.Left,
        desktopRect.Top - monitorBounds.Top,
        desktopRect.Width,
        desktopRect.Height);

    private static double SafeDpi(uint dpi) => dpi == 0 ? DefaultDpi : dpi;
}

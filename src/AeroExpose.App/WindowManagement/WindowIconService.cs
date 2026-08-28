using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AeroExpose.Native;

namespace AeroExpose.WindowManagement;

internal sealed class WindowIconService
{
    private readonly Dictionary<nint, ImageSource?> _cache = new();

    public ImageSource? GetIcon(nint window)
    {
        if (_cache.TryGetValue(window, out var cached))
        {
            return cached;
        }

        var icon = QueryWindowIcon(window);
        ImageSource? source = null;
        if (icon != nint.Zero)
        {
            try
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                    icon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(20, 20));
                bitmap.Freeze();
                source = bitmap;
            }
            catch (ArgumentException)
            {
                source = null;
            }
        }

        _cache[window] = source;
        return source;
    }

    public void Clear() => _cache.Clear();

    private static nint QueryWindowIcon(nint window)
    {
        var icon = NativeMethods.GetClassLongPtr(window, NativeMethods.GetClassLongPtrSmallIcon);
        if (icon != nint.Zero)
        {
            return icon;
        }

        NativeMethods.SendMessageTimeout(
            window,
            NativeMethods.WindowMessageGetIcon,
            NativeMethods.IconSmall2,
            nint.Zero,
            NativeMethods.SendMessageAbortIfHung,
            35,
            out icon);
        if (icon == nint.Zero)
        {
            NativeMethods.SendMessageTimeout(
                window,
                NativeMethods.WindowMessageGetIcon,
                NativeMethods.IconSmall,
                nint.Zero,
                NativeMethods.SendMessageAbortIfHung,
                35,
                out icon);
        }

        if (icon == nint.Zero)
        {
            return NativeMethods.GetClassLongPtr(window, NativeMethods.GetClassLongPtrIcon);
        }

        return icon;
    }
}

using AeroExpose.Core.Models;
using AeroExpose.Native;

namespace AeroExpose.Rendering;

internal sealed class DwmThumbnailManager : IDisposable
{
    private readonly Dictionary<nint, ThumbnailRegistration> _registrations = new();
    private bool _disposed;

    public ThumbnailRegistration? Register(
        nint destinationWindow,
        WindowInfo sourceWindow,
        LayoutRect destination,
        byte opacity = byte.MaxValue)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!NativeMethods.IsWindow(sourceWindow.Handle))
        {
            return null;
        }

        Remove(sourceWindow.Handle);
        var result = NativeMethods.DwmRegisterThumbnail(
            destinationWindow,
            sourceWindow.Handle,
            out var thumbnail);
        if (result < 0 || thumbnail.IsInvalid)
        {
            thumbnail.Dispose();
            return null;
        }

        NativeMethods.DwmQueryThumbnailSourceSize(thumbnail, out var sourceSize);
        var registration = new ThumbnailRegistration(sourceWindow.Handle, thumbnail, sourceSize.Width, sourceSize.Height);
        _registrations[sourceWindow.Handle] = registration;

        if (!Update(sourceWindow.Handle, destination, opacity, true))
        {
            Remove(sourceWindow.Handle);
            return null;
        }

        return registration;
    }

    public bool Update(nint sourceWindow, LayoutRect destination, byte opacity, bool visible)
    {
        if (!_registrations.TryGetValue(sourceWindow, out var registration) || registration.Handle.IsInvalid)
        {
            return false;
        }

        if (!NativeMethods.IsWindow(sourceWindow))
        {
            Remove(sourceWindow);
            return false;
        }

        var properties = new DwmThumbnailProperties
        {
            Flags = NativeMethods.DwmThumbnailDestination |
                NativeMethods.DwmThumbnailOpacity |
                NativeMethods.DwmThumbnailVisible |
                NativeMethods.DwmThumbnailSourceClientAreaOnly,
            Destination = ToNativeRect(destination),
            Opacity = opacity,
            Visible = visible ? 1 : 0,
            SourceClientAreaOnly = 0,
        };

        if (NativeMethods.DwmUpdateThumbnailProperties(registration.Handle, ref properties) >= 0)
        {
            return true;
        }

        Remove(sourceWindow);
        return false;
    }

    public bool IsLive(nint sourceWindow) => _registrations.ContainsKey(sourceWindow);

    public void Remove(nint sourceWindow)
    {
        if (_registrations.Remove(sourceWindow, out var registration))
        {
            registration.Dispose();
        }
    }

    public void Clear()
    {
        foreach (var registration in _registrations.Values)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
    }

    private static NativeRect ToNativeRect(LayoutRect rect)
    {
        var left = (int)Math.Round(rect.X);
        var top = (int)Math.Round(rect.Y);
        var right = Math.Max(left + 1, (int)Math.Round(rect.Right));
        var bottom = Math.Max(top + 1, (int)Math.Round(rect.Bottom));
        return new NativeRect { Left = left, Top = top, Right = right, Bottom = bottom };
    }
}

internal sealed class ThumbnailRegistration : IDisposable
{
    public ThumbnailRegistration(nint sourceWindow, SafeDwmThumbnailHandle handle, int sourceWidth, int sourceHeight)
    {
        SourceWindow = sourceWindow;
        Handle = handle;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
    }

    public nint SourceWindow { get; }

    public SafeDwmThumbnailHandle Handle { get; }

    public int SourceWidth { get; }

    public int SourceHeight { get; }

    public void Dispose() => Handle.Dispose();
}

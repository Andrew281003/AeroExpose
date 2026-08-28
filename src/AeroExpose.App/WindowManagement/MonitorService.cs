using System.Runtime.InteropServices;
using AeroExpose.Core.Models;
using AeroExpose.Native;

namespace AeroExpose.WindowManagement;

internal sealed class MonitorService
{
    public MonitorInfo GetCursorMonitor()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var handle = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MonitorDefaultToNearest);
        return TryGetMonitor(handle) ?? GetAllMonitors().First();
    }

    public IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();
        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (monitor, _, _, _) =>
        {
            var info = TryGetMonitor(monitor);
            if (info is not null)
            {
                monitors.Add(info);
            }

            return true;
        }, nint.Zero);
        return monitors;
    }

    internal static MonitorInfo? TryGetMonitor(nint monitor)
    {
        if (monitor == nint.Zero)
        {
            return null;
        }

        var nativeInfo = new NativeMonitorInfo
        {
            Size = (uint)Marshal.SizeOf<NativeMonitorInfo>(),
            DeviceName = string.Empty,
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref nativeInfo))
        {
            return null;
        }

        uint dpi = 96;
        if (NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) >= 0 && dpiX > 0)
        {
            dpi = dpiX;
        }

        return new MonitorInfo(
            monitor,
            nativeInfo.DeviceName,
            nativeInfo.Monitor.ToPixelRect(),
            nativeInfo.WorkArea.ToPixelRect(),
            dpi,
            (nativeInfo.Flags & NativeMethods.MonitorInfoPrimary) != 0);
    }
}

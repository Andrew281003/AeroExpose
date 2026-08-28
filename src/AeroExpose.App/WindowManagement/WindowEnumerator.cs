using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AeroExpose.Core.Models;
using AeroExpose.Native;

namespace AeroExpose.WindowManagement;

internal sealed class WindowEnumerator
{
    private readonly WindowEligibilityPolicy _eligibilityPolicy;
    private readonly Dictionary<uint, ProcessMetadata> _processMetadata = new();

    public WindowEnumerator(WindowEligibilityPolicy eligibilityPolicy)
    {
        _eligibilityPolicy = eligibilityPolicy;
    }

    public IReadOnlyList<WindowInfo> Enumerate()
    {
        var windows = new List<WindowInfo>();
        var shellWindow = NativeMethods.GetShellWindow();
        var desktopWindow = NativeMethods.GetDesktopWindow();

        NativeMethods.EnumWindows((window, _) =>
        {
            try
            {
                if (window == shellWindow || window == desktopWindow)
                {
                    return true;
                }

                var snapshot = CreateEligibilitySnapshot(window);
                if (!_eligibilityPolicy.IsMissionControlWindow(snapshot))
                {
                    return true;
                }

                var title = GetWindowTitle(snapshot.Handle);
                if (string.IsNullOrWhiteSpace(title) && !_eligibilityPolicy.IncludeUntitledWindows)
                {
                    return true;
                }

                windows.Add(CreateWindowInfo(snapshot, title));
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The target can disappear between any two native calls. It is safe to skip it.
            }

            return true;
        }, nint.Zero);

        return windows;
    }

    private static WindowEligibilitySnapshot CreateEligibilitySnapshot(nint window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        var bounds = GetBestBounds(window);
        var className = GetClassName(window);
        var style = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlStyle).ToInt64();
        var extendedStyle = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExtendedStyle).ToInt64();
        int cloakedValue;
        var cloaked = NativeMethods.DwmGetWindowAttribute(
            window,
            NativeMethods.DwmWindowAttributeCloaked,
            out cloakedValue,
            sizeof(int)) >= 0 && cloakedValue != 0;

        return new WindowEligibilitySnapshot(
            window,
            processId,
            NativeMethods.IsWindowVisible(window),
            NativeMethods.IsIconic(window),
            cloaked,
            className,
            bounds,
            NativeMethods.GetWindow(window, NativeMethods.GetWindowOwner),
            style,
            extendedStyle);
    }

    private WindowInfo CreateWindowInfo(WindowEligibilitySnapshot snapshot, string title)
    {
        var metadata = GetProcessMetadata(snapshot.ProcessId);

        var monitorHandle = NativeMethods.MonitorFromWindow(snapshot.Handle, NativeMethods.MonitorDefaultToNearest);
        var monitorName = MonitorService.TryGetMonitor(monitorHandle)?.DeviceName ?? "Unknown monitor";
        var dpi = NativeMethods.GetDpiForWindow(snapshot.Handle);

        return new WindowInfo(
            snapshot.Handle,
            string.IsNullOrWhiteSpace(title) ? metadata.ApplicationName : title,
            snapshot.ProcessId,
            metadata.ProcessName,
            metadata.ApplicationName,
            metadata.ExecutablePath,
            snapshot.ClassName,
            snapshot.Bounds,
            snapshot.IsMinimized,
            snapshot.IsCloaked,
            snapshot.OwnerHandle,
            monitorHandle,
            monitorName,
            dpi == 0 ? 96u : dpi,
            snapshot.Style,
            snapshot.ExtendedStyle)
        {
            RestoreBounds = GetRestoreBounds(snapshot.Handle, snapshot.Bounds),
        };
    }

    private static PixelRect GetBestBounds(nint window)
    {
        NativeRect frameBounds;
        if (NativeMethods.DwmGetWindowAttribute(
                window,
                NativeMethods.DwmWindowAttributeExtendedFrameBounds,
                out frameBounds,
                Marshal.SizeOf<NativeRect>()) >= 0)
        {
            var rect = frameBounds.ToPixelRect();
            if (!rect.IsEmpty)
            {
                return rect;
            }
        }

        return NativeMethods.GetWindowRect(window, out var windowBounds)
            ? windowBounds.ToPixelRect()
            : default;
    }

    private static string GetWindowTitle(nint window)
    {
        var length = Math.Clamp(NativeMethods.GetWindowTextLength(window), 0, 4096);
        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(window, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static string GetClassName(nint window)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetClassName(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private static PixelRect GetRestoreBounds(nint window, PixelRect fallback)
    {
        var placement = new NativeWindowPlacement
        {
            Length = (uint)Marshal.SizeOf<NativeWindowPlacement>(),
        };
        if (NativeMethods.GetWindowPlacement(window, ref placement))
        {
            var bounds = placement.NormalPosition.ToPixelRect();
            if (!bounds.IsEmpty && bounds.Left > -20_000 && bounds.Top > -20_000)
            {
                return bounds;
            }
        }

        return fallback;
    }

    private ProcessMetadata GetProcessMetadata(uint processId)
    {
        if (_processMetadata.TryGetValue(processId, out var cached))
        {
            return cached;
        }

        var processName = "Application";
        var applicationName = "Application";
        string? executablePath = null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
            applicationName = processName;
            try
            {
                var module = process.MainModule;
                executablePath = module?.FileName;
                var description = module?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    applicationName = description;
                }
            }
            catch (Exception exception) when (
                exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // Protected and packaged applications frequently deny module inspection.
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Process exited after enumeration; fallback metadata remains useful.
        }

        var metadata = new ProcessMetadata(processName, applicationName, executablePath);
        _processMetadata[processId] = metadata;
        return metadata;
    }

    private sealed record ProcessMetadata(string ProcessName, string ApplicationName, string? ExecutablePath);
}

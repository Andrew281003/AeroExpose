using System.Runtime.InteropServices;
using AeroExpose.Native;

namespace AeroExpose.WindowManagement;

internal sealed class WindowActivationService
{
    /// <summary>
    /// Activates after a user click. AttachThreadInput is scoped tightly and always detached;
    /// Windows may still reject foreground transfer, in which case the taskbar is flashed.
    /// </summary>
    public bool TryActivate(nint window)
    {
        if (!NativeMethods.IsWindow(window))
        {
            return false;
        }

        if (NativeMethods.IsIconic(window))
        {
            NativeMethods.ShowWindow(window, NativeMethods.ShowRestore);
        }

        var currentThread = NativeMethods.GetCurrentThreadId();
        var targetThread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = foreground == nint.Zero
            ? 0u
            : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var attachedTarget = false;
        var attachedForeground = false;

        try
        {
            if (targetThread != 0 && targetThread != currentThread)
            {
                attachedTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
            }

            if (foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread)
            {
                attachedForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            }

            NativeMethods.BringWindowToTop(window);
            NativeMethods.SetActiveWindow(window);
            NativeMethods.SetFocus(window);
            if (NativeMethods.SetForegroundWindow(window))
            {
                return true;
            }
        }
        finally
        {
            if (attachedForeground)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }

            if (attachedTarget)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }
        }

        var flash = new FlashWindowInfo
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Window = window,
            Flags = NativeMethods.FlashTray | NativeMethods.FlashTimerNoForeground,
            Count = 3,
        };
        NativeMethods.FlashWindowEx(ref flash);
        return false;
    }
}

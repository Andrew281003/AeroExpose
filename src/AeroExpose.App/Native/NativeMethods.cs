using System.Runtime.InteropServices;
using System.Text;

namespace AeroExpose.Native;

internal static class NativeMethods
{
    internal const int GwlStyle = -16;
    internal const int GwlExtendedStyle = -20;
    internal const long WindowStyleChild = 0x40000000L;
    internal const long WindowStyleCaption = 0x00C00000L;
    internal const long ExtendedStyleToolWindow = 0x00000080L;
    internal const long ExtendedStyleAppWindow = 0x00040000L;
    internal const long ExtendedStyleNoActivate = 0x08000000L;
    internal const uint GetWindowOwner = 4;
    internal const uint MonitorDefaultToNearest = 2;
    internal const uint MonitorInfoPrimary = 1;
    internal const int ShowMinimize = 6;
    internal const int ShowRestore = 9;
    internal const uint FlashTray = 0x00000002;
    internal const uint FlashTimerNoForeground = 0x0000000C;
    internal const uint SendMessageAbortIfHung = 0x0002;
    internal const uint WindowMessageGetIcon = 0x007F;
    internal const int IconSmall = 0;
    internal const int IconSmall2 = 2;
    internal const int GetClassLongPtrSmallIcon = -34;
    internal const int GetClassLongPtrIcon = -14;
    internal const int WindowMessageHotkey = 0x0312;
    internal const int WindowMessageDisplayChange = 0x007E;
    internal const int WindowMessageDpiChanged = 0x02E0;
    internal const uint DwmWindowAttributeExtendedFrameBounds = 9;
    internal const uint DwmWindowAttributeCloaked = 14;
    internal const uint DwmThumbnailDestination = 0x00000001;
    internal const uint DwmThumbnailOpacity = 0x00000004;
    internal const uint DwmThumbnailVisible = 0x00000008;
    internal const uint DwmThumbnailSourceClientAreaOnly = 0x00000010;
    internal const uint DwmBlurBehindEnable = 0x00000001;
    internal const int WindowCompositionAttributeAccentPolicy = 19;
    internal const int AccentDisabled = 0;
    internal const int AccentEnableBlurBehind = 3;
    internal const uint SetWindowPositionShowWindow = 0x0040;
    internal static readonly nint HwndTopmost = new(-1);
    internal static readonly nint HwndMessage = new(-3);

    internal delegate bool EnumWindowsCallback(nint window, nint parameter);

    internal delegate bool MonitorEnumCallback(nint monitor, nint deviceContext, nint monitorRect, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    internal static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    internal static extern nint GetDesktopWindow();

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(nint window, ref NativeWindowPlacement placement);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

    [DllImport("user32.dll")]
    internal static extern nint SetFocus(nint window);

    [DllImport("user32.dll")]
    internal static extern nint SetActiveWindow(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(nint window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    internal static extern nint GetClassLongPtr(nint window, int index);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SendMessageTimeout(
        nint window,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeout,
        out nint result);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref NativeMonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRect,
        MonitorEnumCallback callback,
        nint parameter);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint window);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlashWindowEx(ref FlashWindowInfo info);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(
        nint window,
        uint attribute,
        out NativeRect value,
        int valueSize);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(
        nint window,
        uint attribute,
        out int value,
        int valueSize);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmRegisterThumbnail(
        nint destinationWindow,
        nint sourceWindow,
        out SafeDwmThumbnailHandle thumbnail);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUpdateThumbnailProperties(
        SafeDwmThumbnailHandle thumbnail,
        ref DwmThumbnailProperties properties);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmQueryThumbnailSourceSize(
        SafeDwmThumbnailHandle thumbnail,
        out NativeSize size);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUnregisterThumbnail(nint thumbnail);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmEnableBlurBehindWindow(nint window, ref DwmBlurBehind blurBehind);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmExtendFrameIntoClientArea(nint window, ref NativeMargins margins);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowCompositionAttribute(
        nint window,
        ref WindowCompositionAttributeData data);
}

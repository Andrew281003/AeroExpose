namespace AeroExpose.Core.WindowManagement;

/// <summary>Reusable, platform-value-based eligibility rules for Mission Control windows.</summary>
public static class MissionControlWindowFilter
{
    public const long WindowStyleChild = 0x40000000L;
    public const long ExtendedStyleToolWindow = 0x00000080L;
    public const long ExtendedStyleAppWindow = 0x00040000L;
    public const long ExtendedStyleNoActivate = 0x08000000L;

    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "tooltips_class32",
        "NotifyIconOverflowWindow",
        "TaskListThumbnailWnd",
        "MultitaskingViewFrame",
        "XamlExplorerHostIslandWindow",
    };

    public static bool IsEligible(WindowFilterInput window)
    {
        if (window.Handle == nint.Zero ||
            window.IsCurrentProcess ||
            !window.IsVisible ||
            window.IsCloaked ||
            ExcludedClasses.Contains(window.ClassName))
        {
            return false;
        }

        if ((window.Style & WindowStyleChild) != 0 ||
            window.Bounds.Width < 120 ||
            window.Bounds.Height < 80 ||
            ((long)window.Bounds.Width * window.Bounds.Height) < 15_000)
        {
            return false;
        }

        var isAppWindow = (window.ExtendedStyle & ExtendedStyleAppWindow) != 0;
        var isToolWindow = (window.ExtendedStyle & ExtendedStyleToolWindow) != 0;
        var isNoActivate = (window.ExtendedStyle & ExtendedStyleNoActivate) != 0;
        if ((isToolWindow && !isAppWindow) || (isNoActivate && !isAppWindow))
        {
            return false;
        }

        if (window.OwnerHandle != nint.Zero && !isAppWindow)
        {
            return false;
        }

        return !window.IsMinimized || window.ShowMinimizedWindows;
    }
}

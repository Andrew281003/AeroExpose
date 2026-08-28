using AeroExpose.Core.Models;
using AeroExpose.Core.Settings;
using AeroExpose.Core.WindowManagement;

namespace AeroExpose.WindowManagement;

internal sealed class WindowEligibilityPolicy
{
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;
    private readonly MissionControlSettings _settings;

    public WindowEligibilityPolicy(MissionControlSettings settings)
    {
        _settings = settings;
    }

    public bool IncludeUntitledWindows => _settings.Windows.IncludeUntitled;

    public bool IsMissionControlWindow(WindowEligibilitySnapshot window) =>
        MissionControlWindowFilter.IsEligible(new WindowFilterInput(
            window.Handle,
            window.ProcessId == _currentProcessId,
            window.IsVisible,
            window.IsMinimized,
            window.IsCloaked,
            window.ClassName,
            window.Bounds,
            window.OwnerHandle,
            window.Style,
            window.ExtendedStyle,
            _settings.ShowMinimizedWindows));
}

internal readonly record struct WindowEligibilitySnapshot(
    nint Handle,
    uint ProcessId,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    string ClassName,
    PixelRect Bounds,
    nint OwnerHandle,
    long Style,
    long ExtendedStyle);

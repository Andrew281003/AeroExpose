namespace AeroExpose.Core.Models;

public sealed record MissionControlSession(
    MonitorInfo Monitor,
    IReadOnlyList<WindowInfo> Windows,
    nint PreviouslyActiveWindow);

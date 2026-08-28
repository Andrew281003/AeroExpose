namespace AeroExpose.Core.Models;

public sealed record WindowInfo(
    nint Handle,
    string Title,
    uint ProcessId,
    string ProcessName,
    string ApplicationName,
    string? ExecutablePath,
    string ClassName,
    PixelRect Bounds,
    bool IsMinimized,
    bool IsCloaked,
    nint OwnerHandle,
    nint MonitorHandle,
    string MonitorDeviceName,
    uint Dpi,
    long Style,
    long ExtendedStyle)
{
    public PixelRect RestoreBounds { get; init; } = Bounds;
}

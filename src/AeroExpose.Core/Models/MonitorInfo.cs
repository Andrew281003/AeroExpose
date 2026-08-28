namespace AeroExpose.Core.Models;

public sealed record MonitorInfo(
    nint Handle,
    string DeviceName,
    PixelRect Bounds,
    PixelRect WorkArea,
    uint Dpi,
    bool IsPrimary);

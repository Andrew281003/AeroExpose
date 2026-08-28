using AeroExpose.Core.Models;

namespace AeroExpose.Core.WindowManagement;

public readonly record struct WindowFilterInput(
    nint Handle,
    bool IsCurrentProcess,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    string ClassName,
    PixelRect Bounds,
    nint OwnerHandle,
    long Style,
    long ExtendedStyle,
    bool ShowMinimizedWindows);

using AeroExpose.Core.Models;

namespace AeroExpose.Core.Layout;

public sealed record LayoutPlacement(
    nint WindowHandle,
    LayoutRect Bounds,
    int RowIndex,
    double ScaleFromSource);

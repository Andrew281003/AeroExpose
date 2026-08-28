using AeroExpose.Core.Models;

namespace AeroExpose.Core.Layout;

public sealed record LayoutItem(nint WindowHandle, double AspectRatio, PixelRect SourceBounds)
{
    public double SafeAspectRatio => Math.Clamp(AspectRatio, 0.25d, 6d);
}

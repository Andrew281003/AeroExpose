using AeroExpose.Core.Models;

namespace AeroExpose.Native;

internal static class NativeValueExtensions
{
    internal static PixelRect ToPixelRect(this NativeRect rect) =>
        new(rect.Left, rect.Top, rect.Right, rect.Bottom);
}

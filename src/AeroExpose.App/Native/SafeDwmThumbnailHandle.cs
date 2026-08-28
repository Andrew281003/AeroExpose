using Microsoft.Win32.SafeHandles;

namespace AeroExpose.Native;

/// <summary>Owns a DWM thumbnail relationship and always unregisters it.</summary>
internal sealed class SafeDwmThumbnailHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeDwmThumbnailHandle()
        : base(true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.DwmUnregisterThumbnail(handle) >= 0;
}

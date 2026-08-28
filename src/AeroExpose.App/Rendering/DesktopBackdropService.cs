using System.Runtime.InteropServices;
using AeroExpose.Native;

namespace AeroExpose.Rendering;

/// <summary>
/// Applies the best Windows 10 backdrop available. The documented DWM blur call is always
/// configured; the Windows 10 accent policy is an isolated enhancement and can fail safely.
/// </summary>
internal sealed class DesktopBackdropService
{
    public bool Apply(nint window, bool blurEnabled)
    {
        var margins = new NativeMargins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(window, ref margins);

        var blur = new DwmBlurBehind
        {
            Flags = NativeMethods.DwmBlurBehindEnable,
            Enable = blurEnabled ? 1 : 0,
        };
        var documentedResult = NativeMethods.DwmEnableBlurBehindWindow(window, ref blur) >= 0;
        return TrySetAccent(window, blurEnabled) || documentedResult;
    }

    public void Remove(nint window)
    {
        TrySetAccent(window, false);
        var blur = new DwmBlurBehind
        {
            Flags = NativeMethods.DwmBlurBehindEnable,
            Enable = 0,
        };
        NativeMethods.DwmEnableBlurBehindWindow(window, ref blur);
    }

    private static bool TrySetAccent(nint window, bool enabled)
    {
        var policy = new AccentPolicy
        {
            AccentState = enabled
                ? NativeMethods.AccentEnableBlurBehind
                : NativeMethods.AccentDisabled,
            AccentFlags = enabled ? 2 : 0,
            GradientColor = 0x280C0A08,
        };
        var size = Marshal.SizeOf<AccentPolicy>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, pointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = NativeMethods.WindowCompositionAttributeAccentPolicy,
                Data = pointer,
                Size = (nuint)size,
            };
            return NativeMethods.SetWindowCompositionAttribute(window, ref data);
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}

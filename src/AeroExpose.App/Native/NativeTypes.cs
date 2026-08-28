using System.Runtime.InteropServices;

namespace AeroExpose.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSize
{
    public int Width;
    public int Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DwmThumbnailProperties
{
    public uint Flags;
    public NativeRect Destination;
    public NativeRect Source;
    public byte Opacity;
    public int Visible;
    public int SourceClientAreaOnly;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeMonitorInfo
{
    public uint Size;
    public NativeRect Monitor;
    public NativeRect WorkArea;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FlashWindowInfo
{
    public uint Size;
    public nint Window;
    public uint Flags;
    public uint Count;
    public uint Timeout;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeWindowPlacement
{
    public uint Length;
    public uint Flags;
    public uint ShowCommand;
    public NativePoint MinimumPosition;
    public NativePoint MaximumPosition;
    public NativeRect NormalPosition;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DwmBlurBehind
{
    public uint Flags;
    public int Enable;
    public nint BlurRegion;
    public int TransitionOnMaximized;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMargins
{
    public int Left;
    public int Right;
    public int Top;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AccentPolicy
{
    public int AccentState;
    public int AccentFlags;
    public uint GradientColor;
    public int AnimationId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowCompositionAttributeData
{
    public int Attribute;
    public nint Data;
    public nuint Size;
}

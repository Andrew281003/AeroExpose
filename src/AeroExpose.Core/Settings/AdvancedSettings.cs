namespace AeroExpose.Core.Settings;

public sealed class AdvancedSettings
{
    public bool LiveDwmThumbnails { get; set; } = true;
    public bool HardwareAcceleration { get; set; } = true;
    public FpsTarget FpsTarget { get; set; } = FpsTarget.Display;
    public bool DebugMode { get; set; }
}

public enum FpsTarget { Automatic, Fps60, Fps120, Fps144, Display }

namespace AeroExpose.Core.Settings;

public sealed class GeneralSettings
{
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public LaunchBehavior LaunchBehavior { get; set; } = LaunchBehavior.Silent;
    public bool ShowTrayIcon { get; set; } = true;
}

public enum LaunchBehavior { Silent, OpenSettings }

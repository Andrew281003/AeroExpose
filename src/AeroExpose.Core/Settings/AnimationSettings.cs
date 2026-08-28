using System.Text.Json.Serialization;

namespace AeroExpose.Core.Settings;

public sealed class AnimationSettings
{
    public bool Enabled { get; set; } = true;
    public int DurationMs { get; set; } = 320;
    public bool UseSeparateSpeeds { get; set; }
    public int OpenDurationMs { get; set; } = 320;
    public int CloseDurationMs { get; set; } = 260;
    public AnimationStyle Style { get; set; } = AnimationStyle.Smooth;
    public bool Stagger { get; set; }
    public bool ReduceMotion { get; set; }

    [JsonIgnore] public int EffectiveOpenDurationMs => UseSeparateSpeeds ? OpenDurationMs : DurationMs;
    [JsonIgnore] public int EffectiveCloseDurationMs => UseSeparateSpeeds ? CloseDurationMs : DurationMs;

    internal void Normalize()
    {
        DurationMs = Math.Clamp(DurationMs, 100, 800);
        OpenDurationMs = Math.Clamp(OpenDurationMs, 100, 800);
        CloseDurationMs = Math.Clamp(CloseDurationMs, 100, 800);
    }
}

public enum AnimationStyle { Smooth, Spring, Snappy, Linear }

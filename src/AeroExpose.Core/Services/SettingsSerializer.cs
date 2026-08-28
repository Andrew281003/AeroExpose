using System.Text.Json;
using System.Text.Json.Serialization;
using AeroExpose.Core.Settings;

namespace AeroExpose.Core.Services;

public static class SettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(MissionControlSettings settings) =>
        JsonSerializer.Serialize(settings, Options);

    public static MissionControlSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<MissionControlSettings>(json, Options)
            ?? new MissionControlSettings();
        using var document = JsonDocument.Parse(json);
        if (!TryGetProperty(document.RootElement, "general", out _))
        {
            MigrateLegacySettings(document.RootElement, settings);
        }
        settings.Normalize();
        return settings;
    }

    private static void MigrateLegacySettings(JsonElement root, MissionControlSettings settings)
    {
        if (TryGetProperty(root, "shortcutModifiers", out var modifiers) && modifiers.ValueKind == JsonValueKind.String &&
            Enum.TryParse<HotkeyModifiers>(modifiers.GetString(), ignoreCase: true, out var parsedModifiers))
            settings.ShortcutModifiers = parsedModifiers;
        if (TryGetProperty(root, "shortcutVirtualKey", out var key) && key.TryGetUInt32(out var virtualKey))
            settings.ShortcutVirtualKey = virtualKey;
        if (TryGetProperty(root, "animationDurationMilliseconds", out var duration) && duration.TryGetInt32(out var durationMs))
            settings.AnimationDurationMilliseconds = durationMs;
        if (TryGetProperty(root, "closeAnimationDurationMilliseconds", out var closeDuration) && closeDuration.TryGetInt32(out var closeDurationMs))
            settings.Animations.CloseDurationMs = closeDurationMs;
        if (TryGetProperty(root, "backgroundDimAmount", out var dim) && dim.TryGetDouble(out var dimAmount))
            settings.BackgroundDimAmount = dimAmount;
        if (TryGetProperty(root, "blurEnabled", out var blur) && blur.ValueKind is JsonValueKind.True or JsonValueKind.False)
            settings.BlurEnabled = blur.GetBoolean();
        if (TryGetProperty(root, "showWindowTitles", out var titles) && titles.ValueKind is JsonValueKind.True or JsonValueKind.False)
            settings.ShowWindowTitles = titles.GetBoolean();
        if (TryGetProperty(root, "previewSpacing", out var spacing) && spacing.TryGetDouble(out var spacingValue))
            settings.PreviewSpacing = spacingValue;
        if (TryGetProperty(root, "hoverScale", out var hoverScale) && hoverScale.TryGetDouble(out var hoverScaleValue))
            settings.HoverScale = hoverScaleValue;
        if (TryGetProperty(root, "showMinimizedWindows", out var minimized) && minimized.ValueKind is JsonValueKind.True or JsonValueKind.False)
            settings.ShowMinimizedWindows = minimized.GetBoolean();
        if (TryGetProperty(root, "monitorMode", out var monitor) && monitor.ValueKind == JsonValueKind.String &&
            Enum.TryParse<MonitorMode>(monitor.GetString(), ignoreCase: true, out var monitorMode))
            settings.MonitorMode = monitorMode;
        if (TryGetProperty(root, "debugOverlayEnabled", out var debug) && debug.ValueKind is JsonValueKind.True or JsonValueKind.False)
            settings.DebugOverlayEnabled = debug.GetBoolean();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}

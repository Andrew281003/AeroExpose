using AeroExpose.Core.Layout;
using AeroExpose.Core.Models;
using AeroExpose.Core.Services;
using AeroExpose.Core.Settings;
using AeroExpose.Core.Utilities;
using AeroExpose.Core.WindowManagement;

var tests = new (string Name, Action Test)[]
{
    ("DPI conversion round trips at common scales", DpiRoundTrip),
    ("Desktop coordinates translate to overlay coordinates", CoordinateTranslation),
    ("Settings JSON round trips and normalizes", SettingsRoundTrip),
    ("Settings defaults match background utility UX", SettingsDefaults),
    ("Legacy flat settings JSON migrates", LegacySettingsMigration),
    ("Layout handles Mission Control window counts", LayoutScenarios),
    ("Layout preserves extreme aspect ratios", LayoutExtremeAspects),
    ("Window filter accepts normal application windows", WindowFilterAcceptsApplications),
    ("Window filter rejects shell, helper, and hidden windows", WindowFilterRejectsNoise),
};

var failures = new List<string>();
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL  {name}: {exception.Message}");
        Console.WriteLine(failures[^1]);
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
return failures.Count == 0 ? 0 : 1;

static void DpiRoundTrip()
{
    foreach (var dpi in new uint[] { 96, 120, 144, 168, 192 })
    {
        const double pixels = 731.25d;
        var roundTrip = DpiMath.DipsToPixels(DpiMath.PixelsToDips(pixels, dpi), dpi);
        AssertNear(pixels, roundTrip, 0.0001d);
    }
}

static void CoordinateTranslation()
{
    var monitor = new PixelRect(-2560, 0, 0, 1440);
    var window = new PixelRect(-2400, 120, -1120, 840);
    var translated = DpiMath.DesktopPixelsToOverlayPixels(window, monitor);
    AssertEqual(new LayoutRect(160, 120, 1280, 720), translated);
}

static void SettingsRoundTrip()
{
    var source = new MissionControlSettings
    {
        AnimationDurationMilliseconds = 341,
        BackgroundDimAmount = 0.73d,
        MonitorMode = MonitorMode.AllWindowsOnCursorMonitor,
        HoverScale = 4d,
    };
    var result = SettingsSerializer.Deserialize(SettingsSerializer.Serialize(source));
    AssertEqual(341, result.AnimationDurationMilliseconds);
    AssertNear(0.73d, result.BackgroundDimAmount, 0.0001d);
    AssertEqual(MonitorMode.AllWindowsOnCursorMonitor, result.MonitorMode);
    AssertNear(1.12d, result.HoverScale, 0.0001d);
}

static void SettingsDefaults()
{
    var settings = new MissionControlSettings();
    AssertTrue(settings.General.Enabled, "AeroExpose should be enabled by default.");
    AssertTrue(!settings.General.StartWithWindows, "Startup registration should be opt-in.");
    AssertEqual(LaunchBehavior.Silent, settings.General.LaunchBehavior);
    AssertTrue(settings.General.ShowTrayIcon, "The tray icon should be visible by default.");
    AssertEqual(320, settings.Animations.DurationMs);
    AssertEqual(AnimationStyle.Smooth, settings.Animations.Style);
    AssertNear(0.35d, settings.Appearance.DimStrength, 0.0001d);
}

static void LegacySettingsMigration()
{
    const string json = """
        { "AnimationDurationMilliseconds": 450, "BackgroundDimAmount": 0.44,
          "ShowWindowTitles": false, "PreviewSpacing": 42, "HoverScale": 1.08,
          "MonitorMode": "CursorMonitor" }
        """;
    var settings = SettingsSerializer.Deserialize(json);
    AssertEqual(450, settings.Animations.DurationMs);
    AssertNear(0.44d, settings.Appearance.DimStrength, 0.0001d);
    AssertTrue(!settings.Appearance.ShowTitles, "Legacy title visibility should migrate.");
    AssertNear(1.5d, settings.Windows.Spacing, 0.0001d);
    AssertNear(1.08d, settings.Appearance.HoverScale, 0.0001d);
    AssertTrue(!settings.Windows.ShowAllMonitors, "Legacy monitor mode should migrate.");
}

static void LayoutScenarios()
{
    foreach (var count in new[] { 1, 2, 3, 5, 10, 15, 21 })
    {
        var aspects = Enumerable.Range(0, count)
            .Select(index => index % 4 switch
            {
                0 => 16d / 9d,
                1 => 4d / 3d,
                2 => 3d / 4d,
                _ => 21d / 9d,
            })
            .ToArray();
        var result = Arrange(aspects);
        AssertEqual(count, result.Placements.Count);
        AssertTrue(result.Score > 0d, $"Expected a positive score for {count} windows.");
        AssertValidPlacement(result, aspects, new LayoutRect(0, 0, 1920, 1040));
    }
}

static void LayoutExtremeAspects()
{
    var aspects = new[] { 4.8d, 0.28d, 16d / 9d, 1d, 2.4d, 0.5d, 1.6d };
    var result = Arrange(aspects);
    AssertEqual(aspects.Length, result.Placements.Count);
    AssertValidPlacement(result, aspects, new LayoutRect(0, 0, 1920, 1040));
    AssertTrue(result.RowCount > 1, "Mixed extreme windows should not collapse into one row.");
}

static void WindowFilterAcceptsApplications()
{
    var normal = CreateFilterInput();
    AssertTrue(MissionControlWindowFilter.IsEligible(normal), "A normal top-level window should be eligible.");

    var blankTitledEquivalent = normal with { ClassName = "Chrome_WidgetWin_1" };
    AssertTrue(MissionControlWindowFilter.IsEligible(blankTitledEquivalent), "Eligibility must not depend on title text.");

    var explicitOwnedApp = normal with
    {
        OwnerHandle = (nint)55,
        ExtendedStyle = MissionControlWindowFilter.ExtendedStyleAppWindow,
    };
    AssertTrue(MissionControlWindowFilter.IsEligible(explicitOwnedApp), "WS_EX_APPWINDOW should retain an intentional owned app window.");

    var minimizedAllowed = normal with { IsMinimized = true, ShowMinimizedWindows = true };
    AssertTrue(MissionControlWindowFilter.IsEligible(minimizedAllowed), "Configured minimized windows should remain eligible.");
}

static void WindowFilterRejectsNoise()
{
    var normal = CreateFilterInput();
    var rejected = new[]
    {
        normal with { IsVisible = false },
        normal with { IsCloaked = true },
        normal with { IsCurrentProcess = true },
        normal with { ClassName = "Shell_TrayWnd" },
        normal with { ClassName = "tooltips_class32" },
        normal with { Bounds = new PixelRect(0, 0, 80, 40) },
        normal with { Style = MissionControlWindowFilter.WindowStyleChild },
        normal with { ExtendedStyle = MissionControlWindowFilter.ExtendedStyleToolWindow },
        normal with { ExtendedStyle = MissionControlWindowFilter.ExtendedStyleNoActivate },
        normal with { OwnerHandle = (nint)99 },
        normal with { IsMinimized = true, ShowMinimizedWindows = false },
    };

    foreach (var candidate in rejected)
    {
        AssertTrue(!MissionControlWindowFilter.IsEligible(candidate), $"Expected rejection for {candidate}.");
    }
}

static WindowFilterInput CreateFilterInput() => new(
    Handle: (nint)42,
    IsCurrentProcess: false,
    IsVisible: true,
    IsMinimized: false,
    IsCloaked: false,
    ClassName: "NormalApplicationWindow",
    Bounds: new PixelRect(100, 100, 1380, 820),
    OwnerHandle: nint.Zero,
    Style: 0,
    ExtendedStyle: 0,
    ShowMinimizedWindows: true);

static LayoutResult Arrange(IReadOnlyList<double> aspects)
{
    var items = aspects.Select((aspect, index) => new LayoutItem(
        (nint)(index + 1),
        aspect,
        new PixelRect((index % 4) * 420, (index / 4) * 280, ((index % 4) * 420) + 1280, ((index / 4) * 280) + 720)))
        .ToArray();
    return new MissionControlLayoutEngine().Arrange(
        items,
        new LayoutRect(0, 0, 1920, 1040),
        new LayoutOptions(60, 28, 34));
}

static void AssertValidPlacement(
    LayoutResult result,
    IReadOnlyList<double> sourceAspects,
    LayoutRect availableArea)
{
    foreach (var placement in result.Placements)
    {
        AssertTrue(placement.Bounds.X >= availableArea.X, "Placement extends beyond the left edge.");
        AssertTrue(placement.Bounds.Y >= availableArea.Y, "Placement extends beyond the top edge.");
        AssertTrue(placement.Bounds.Right <= availableArea.Right + 0.001d, "Placement extends beyond the right edge.");
        AssertTrue(placement.Bounds.Bottom <= availableArea.Bottom + 0.001d, "Placement extends beyond the bottom edge.");
        var expectedAspect = Math.Clamp(sourceAspects[placement.WindowHandle.ToInt32() - 1], 0.25d, 6d);
        AssertNear(expectedAspect, placement.Bounds.Width / placement.Bounds.Height, 0.0001d);
    }

    for (var first = 0; first < result.Placements.Count; first++)
    {
        for (var second = first + 1; second < result.Placements.Count; second++)
        {
            var a = result.Placements[first].Bounds;
            var b = result.Placements[second].Bounds;
            var overlaps = a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
            AssertTrue(!overlaps, $"Placements {first} and {second} overlap.");
        }
    }
}

static void AssertNear(double expected, double actual, double tolerance)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertEqual<T>(T expected, T actual)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

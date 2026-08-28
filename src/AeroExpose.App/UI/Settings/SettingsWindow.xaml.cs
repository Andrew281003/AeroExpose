using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AeroExpose.Core.Settings;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Panel = System.Windows.Controls.Panel;
using TextBox = System.Windows.Controls.TextBox;

namespace AeroExpose.UI.Settings;

public partial class SettingsWindow : Window
{
    private readonly MissionControlSettings _settings;
    private readonly Func<Task> _saveAsync;
    private readonly Func<HotkeyModifiers, uint, bool> _tryChangeHotkey;
    private readonly Action _settingsChanged;
    private bool _loading = true;
    private Panel? _animationPreview;
    private TextBlock? _durationText;

    public SettingsWindow(
        MissionControlSettings settings,
        Func<Task> saveAsync,
        Func<HotkeyModifiers, uint, bool> tryChangeHotkey,
        Action settingsChanged)
    {
        _settings = settings;
        _saveAsync = saveAsync;
        _tryChangeHotkey = tryChangeHotkey;
        _settingsChanged = settingsChanged;
        InitializeComponent();
        RestoreWindowState();
        Navigation.SelectedIndex = Math.Max(0, PageNames.IndexOf(_settings.SettingsWindow.SelectedPage));
        _loading = false;
        ShowPage(_settings.SettingsWindow.SelectedPage);
        ApplyTheme();
    }

    private static List<string> PageNames { get; } = ["General", "Appearance", "Animations", "Input", "Windows", "Advanced", "About"];

    private void OnNavigationChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (Navigation.SelectedItem is ListBoxItem { Content: string page })
        {
            _settings.SettingsWindow.SelectedPage = page;
            ShowPage(page);
            Changed();
        }
    }

    private void ShowPage(string page)
    {
        PageTitle.Text = page;
        PageContent.Children.Clear();
        _animationPreview = null;
        switch (page)
        {
            case "General": BuildGeneral(); break;
            case "Appearance": BuildAppearance(); break;
            case "Animations": BuildAnimations(); break;
            case "Input": BuildInput(); break;
            case "Windows": BuildWindows(); break;
            case "Advanced": BuildAdvanced(); break;
            case "About": BuildAbout(); break;
        }
    }

    private void BuildGeneral()
    {
        AddDescription("Control the background utility and how it starts.");
        AddCheck("Enable AeroExpose", _settings.General.Enabled, value => _settings.General.Enabled = value);
        AddCheck("Start AeroExpose when I sign in", _settings.General.StartWithWindows, value => _settings.General.StartWithWindows = value);
        AddCombo("When AeroExpose starts", _settings.General.LaunchBehavior, value => _settings.General.LaunchBehavior = value);
        AddCheck("Show AeroExpose in system tray", _settings.General.ShowTrayIcon, value => _settings.General.ShowTrayIcon = value);
        AddNote("If the tray icon is hidden, launch AeroExpose again to reopen Settings. The existing background instance will handle it.");
    }

    private void BuildAppearance()
    {
        AddCombo("Theme", _settings.Appearance.Theme, value => { _settings.Appearance.Theme = value; ApplyTheme(); });
        AddCombo("Background effect", _settings.Appearance.BackgroundEffect, value => _settings.Appearance.BackgroundEffect = value);
        AddSlider("Background dim strength", _settings.Appearance.DimStrength * 100, 0, 80, "%", value => _settings.Appearance.DimStrength = value / 100);
        AddNote("Windows 10 controls blur intensity through DWM; AeroExpose does not expose a fake blur-strength slider.");
        AddCombo("Window preview corners", _settings.Appearance.PreviewCorners, value => _settings.Appearance.PreviewCorners = value);
        AddCheck("Window preview shadows", _settings.Appearance.PreviewShadow, value => _settings.Appearance.PreviewShadow = value);
        AddCheck("Show window titles", _settings.Appearance.ShowTitles, value => _settings.Appearance.ShowTitles = value);
        AddCheck("Show application icons", _settings.Appearance.ShowIcons, value => _settings.Appearance.ShowIcons = value);
        AddCombo("Hover effect", _settings.Appearance.HoverEffect, value => _settings.Appearance.HoverEffect = value);
    }

    private void BuildAnimations()
    {
        AddCheck("Enable animations", _settings.Animations.Enabled, value => { _settings.Animations.Enabled = value; ReplayPreview(); });
        var duration = AddSlider("Animation speed", _settings.Animations.DurationMs, 100, 800, " ms", value => { _settings.Animations.DurationMs = (int)value; UpdateDurationText(); ReplayPreview(); });
        duration.IsDirectionReversed = true;
        _durationText = AddNote($"Animation duration: {_settings.Animations.DurationMs} ms");
        AddCheck("Use separate opening and closing speeds", _settings.Animations.UseSeparateSpeeds, value => { _settings.Animations.UseSeparateSpeeds = value; ShowPage("Animations"); });
        if (_settings.Animations.UseSeparateSpeeds)
        {
            AddSlider("Opening animation speed", _settings.Animations.OpenDurationMs, 100, 800, " ms", value => _settings.Animations.OpenDurationMs = (int)value).IsDirectionReversed = true;
            AddSlider("Closing animation speed", _settings.Animations.CloseDurationMs, 100, 800, " ms", value => _settings.Animations.CloseDurationMs = (int)value).IsDirectionReversed = true;
        }
        AddCombo("Animation style", _settings.Animations.Style, value => { _settings.Animations.Style = value; ReplayPreview(); });
        AddCheck("Slightly stagger window animations", _settings.Animations.Stagger, value => { _settings.Animations.Stagger = value; ReplayPreview(); });
        AddCheck("Reduce motion", _settings.Animations.ReduceMotion, value => { _settings.Animations.ReduceMotion = value; ReplayPreview(); });

        AddHeader("Live preview");
        var preview = new Grid { Height = 180, Background = BrushFrom("#191D25"), Margin = new Thickness(0, 8, 0, 8) };
        for (var index = 0; index < 4; index++)
        {
            preview.Children.Add(new Border
            {
                Width = 116, Height = 72, CornerRadius = new CornerRadius(8), Background = BrushFrom(index % 2 == 0 ? "#3979D8" : "#7357C8"),
                BorderBrush = BrushFrom("#88FFFFFF"), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(28 + ((index % 2) * 138), 18 + ((index / 2) * 82), 0, 0),
                RenderTransform = new ScaleTransform(0.72, 0.72), RenderTransformOrigin = new Point(0.5, 0.5), Opacity = 0,
            });
        }
        PageContent.Children.Add(preview);
        _animationPreview = preview;
        var test = new Button { Content = "Test Animation", HorizontalAlignment = HorizontalAlignment.Left };
        test.Click += (_, _) => ReplayPreview();
        PageContent.Children.Add(test);
        Dispatcher.BeginInvoke(ReplayPreview);
    }

    private void BuildInput()
    {
        AddHeader("Mission Control Shortcut");
        AddDescription("Click the shortcut field, then press a new key combination.");
        var shortcut = new TextBox
        {
            Text = FormatShortcut(), IsReadOnly = true, Height = 38, Width = 250, Padding = new Thickness(10, 7, 10, 7),
            HorizontalAlignment = HorizontalAlignment.Left, Background = BrushFrom("#242832"), Foreground = Foreground,
        };
        shortcut.PreviewKeyDown += (_, eventArgs) => CaptureShortcut(shortcut, eventArgs);
        PageContent.Children.Add(shortcut);
        AddCombo("Four-finger swipe up (Experimental)", _settings.Input.FourFingerSwipeUp, value => _settings.Input.FourFingerSwipeUp = value);
        AddNote("Trackpad support depends on Windows Advanced gesture configuration; AeroExpose does not claim native Precision Touchpad capture.");
        AddCombo("Pressing Escape", _settings.Input.EscapeBehavior, value => _settings.Input.EscapeBehavior = value);
    }

    private void BuildWindows()
    {
        AddCheck("Show minimized windows", _settings.Windows.ShowMinimized, value => _settings.Windows.ShowMinimized = value);
        AddCheck("Show windows from all monitors", _settings.Windows.ShowAllMonitors, value => _settings.Windows.ShowAllMonitors = value);
        AddCheck("Show window titles", _settings.Windows.ShowTitles, value => _settings.Windows.ShowTitles = value);
        AddCheck("Show application icons", _settings.Windows.ShowIcons, value => _settings.Windows.ShowIcons = value);
        AddCheck("Include windows without titles", _settings.Windows.IncludeUntitled, value => _settings.Windows.IncludeUntitled = value);
        AddSlider("Preview size", _settings.Windows.PreviewScale, 0.75, 1.25, "×", value => _settings.Windows.PreviewScale = value);
        AddSlider("Window spacing", _settings.Windows.Spacing, 0.5, 1.75, "×", value => _settings.Windows.Spacing = value);
    }

    private void BuildAdvanced()
    {
        AddCheck("Use live DWM thumbnails", _settings.Advanced.LiveDwmThumbnails, value => _settings.Advanced.LiveDwmThumbnails = value);
        AddCheck("Hardware accelerated animations", _settings.Advanced.HardwareAcceleration, value => _settings.Advanced.HardwareAcceleration = value);
        AddCombo("Animation FPS target", _settings.Advanced.FpsTarget, value => _settings.Advanced.FpsTarget = value);
        AddNote("WPF synchronizes animation to the desktop compositor. FPS choices are stored for future renderers and do not impose a fake timer limit.");
        AddCheck("Enable debug information", _settings.Advanced.DebugMode, value => _settings.Advanced.DebugMode = value);
    }

    private void BuildAbout()
    {
        AddHeader("AeroExpose");
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "Unknown";
        AddDescription($"Version: {version}");
        AddDescription("A modern Mission Control-style window overview for Windows.");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 15, 0, 0) };
        row.Children.Add(LinkButton("Check for Updates"));
        row.Children.Add(LinkButton("Open Project Page"));
        var licenses = new Button { Content = "View Licenses" };
        licenses.Click += (_, _) => MessageBox.Show(this, "AeroExpose uses the .NET runtime and Windows platform APIs. No third-party packages are bundled.", "Licenses");
        row.Children.Add(licenses);
        PageContent.Children.Add(row);
    }

    private Button LinkButton(string label)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) =>
        {
            var url = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "RepositoryUrl")?.Value;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(this, "No project page is present in this build's assembly metadata.", "AeroExpose");
            }
        };
        return button;
    }

    private void CaptureShortcut(TextBox field, KeyEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }
        var modifiers = HotkeyModifiers.NoRepeat;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Windows;
        if ((modifiers & ~HotkeyModifiers.NoRepeat) == 0 || key is Key.Escape or Key.Tab or Key.Delete)
        {
            MessageBox.Show(this, "Choose a shortcut with Ctrl, Alt, Shift, or Windows and a non-reserved key.", "Invalid shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!_tryChangeHotkey(modifiers, virtualKey))
        {
            MessageBox.Show(this, "That shortcut could not be registered. The previous shortcut is still active.", "Shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _settings.Input.ShortcutModifiers = modifiers;
        _settings.Input.ShortcutVirtualKey = virtualKey;
        field.Text = FormatShortcut();
        Changed();
    }

    private string FormatShortcut()
    {
        var parts = new List<string>();
        var modifiers = _settings.Input.ShortcutModifiers;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)_settings.Input.ShortcutVirtualKey).ToString());
        return string.Join(" + ", parts);
    }

    private void ReplayPreview()
    {
        if (_animationPreview is null) return;
        var duration = TimeSpan.FromMilliseconds(_settings.Animations.ReduceMotion ? 140 : _settings.Animations.DurationMs);
        for (var index = 0; index < _animationPreview.Children.Count; index++)
        {
            if (_animationPreview.Children[index] is not Border item || item.RenderTransform is not ScaleTransform scale) continue;
            item.BeginAnimation(OpacityProperty, null); scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            if (!_settings.Animations.Enabled) { item.Opacity = 1; scale.ScaleX = scale.ScaleY = 1; continue; }
            var begin = _settings.Animations.Stagger && !_settings.Animations.ReduceMotion ? TimeSpan.FromMilliseconds(index * 24) : TimeSpan.Zero;
            var easing = CreatePreviewEasing();
            item.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { BeginTime = begin, EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd });
            if (!_settings.Animations.ReduceMotion)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.72, 1, duration) { BeginTime = begin, EasingFunction = easing });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.72, 1, duration) { BeginTime = begin, EasingFunction = easing });
            }
            else { scale.ScaleX = scale.ScaleY = 1; }
        }
    }

    private IEasingFunction CreatePreviewEasing() => _settings.Animations.Style switch
    {
        AnimationStyle.Spring when !_settings.Animations.ReduceMotion => new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut },
        AnimationStyle.Snappy => new CubicEase { EasingMode = EasingMode.EaseOut },
        AnimationStyle.Linear => new PowerEase { Power = 1, EasingMode = EasingMode.EaseIn },
        _ => new CubicEase { EasingMode = EasingMode.EaseInOut },
    };

    private void AddHeader(string text) => PageContent.Children.Add(new TextBlock { Text = text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 5) });
    private void AddDescription(string text) => PageContent.Children.Add(new TextBlock { Text = text, Foreground = BrushFrom("#B9C0CC"), FontSize = 14, Margin = new Thickness(0, 0, 0, 12) });
    private TextBlock AddNote(string text) { var block = new TextBlock { Text = text, Foreground = BrushFrom("#8993A3"), FontSize = 12, Margin = new Thickness(0, 3, 0, 10) }; PageContent.Children.Add(block); return block; }

    private void AddCheck(string label, bool value, Action<bool> update)
    {
        var control = new CheckBox { Content = label, IsChecked = value };
        control.Click += (_, _) => { update(control.IsChecked == true); Changed(); };
        PageContent.Children.Add(control);
    }

    private void AddCombo<T>(string label, T value, Action<T> update) where T : struct, Enum
    {
        AddHeader(label);
        var control = new ComboBox { ItemsSource = Enum.GetValues<T>(), SelectedItem = value, HorizontalAlignment = HorizontalAlignment.Left };
        control.SelectionChanged += (_, _) => { if (control.SelectedItem is T selected) { update(selected); Changed(); } };
        PageContent.Children.Add(control);
    }

    private Slider AddSlider(string label, double value, double minimum, double maximum, string suffix, Action<double> update)
    {
        AddHeader(label);
        var valueText = AddNote($"{value:0.##}{suffix}");
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, TickFrequency = (maximum - minimum) / 20, IsSnapToTickEnabled = false };
        slider.ValueChanged += (_, _) => { valueText.Text = $"{slider.Value:0.##}{suffix}"; update(slider.Value); Changed(); };
        PageContent.Children.Add(slider);
        return slider;
    }

    private void Changed()
    {
        if (_loading) return;
        _settings.Normalize();
        _settingsChanged();
        _ = _saveAsync();
    }

    private void UpdateDurationText() { if (_durationText is not null) _durationText.Text = $"Animation duration: {_settings.Animations.DurationMs} ms"; }

    private void ApplyTheme()
    {
        var light = _settings.Appearance.Theme == AeroExpose.Core.Settings.ThemeMode.Light ||
            (_settings.Appearance.Theme == AeroExpose.Core.Settings.ThemeMode.System && IsSystemLightTheme());
        Background = BrushFrom(light ? "#F4F5F7" : "#111318");
        Foreground = BrushFrom(light ? "#171A20" : "#F3F5F8");
        Sidebar.Background = BrushFrom(light ? "#E7E9ED" : "#191C23");
        Sidebar.BorderBrush = BrushFrom(light ? "#D0D4DB" : "#2A2E38");
        Navigation.Foreground = Foreground;
    }

    private void RestoreWindowState()
    {
        Width = _settings.SettingsWindow.Width; Height = _settings.SettingsWindow.Height;
        if (_settings.SettingsWindow.Left is double left && _settings.SettingsWindow.Top is double top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual; Left = left; Top = top;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs eventArgs)
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.SettingsWindow.Left = Left; _settings.SettingsWindow.Top = Top;
            _settings.SettingsWindow.Width = Width; _settings.SettingsWindow.Height = Height;
        }
        _ = _saveAsync();
    }

    private static SolidColorBrush BrushFrom(string color) => new((Color)ColorConverter.ConvertFromString(color));

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

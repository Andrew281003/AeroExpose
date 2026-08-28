using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using AeroExpose.Animation;
using AeroExpose.AppServices;
using AeroExpose.Core.Layout;
using AeroExpose.Core.Settings;
using AeroExpose.Diagnostics;
using AeroExpose.Input;
using AeroExpose.Rendering;
using AeroExpose.Services;
using AeroExpose.Tray;
using AeroExpose.UI;
using AeroExpose.UI.Settings;
using AeroExpose.WindowManagement;

namespace AeroExpose.AppServices;

internal sealed class BackgroundApplicationHost : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SettingsService _settingsService;
    private readonly StartupRegistrationService _startupService = new();
    private readonly Action _requestExit;
    private readonly bool _manageStartupRegistration;
    private readonly GlobalHotkeyManager _hotkeyManager;
    private readonly TrayIconService _trayIcon;
    private readonly SemaphoreSlim _startupUpdateLock = new(1, 1);
    private SettingsWindow? _settingsWindow;
    private bool? _lastStartWithWindows;
    private bool _disposed;

    public BackgroundApplicationHost(
        MissionControlSettings settings,
        SettingsService settingsService,
        Dispatcher dispatcher,
        Action requestExit,
        bool registerHotkey = true,
        bool manageStartupRegistration = true)
    {
        Settings = settings;
        _settingsService = settingsService;
        _dispatcher = dispatcher;
        _requestExit = requestExit;
        _manageStartupRegistration = manageStartupRegistration;

        SingleInstanceService.Trace("Creating Mission Control services.");
        MonitorService = new MonitorService();
        WindowEnumerator = new WindowEnumerator(new WindowEligibilityPolicy(settings));
        var overlay = new MissionControlOverlay(
            settings,
            new MissionControlLayoutEngine(),
            new AnimationService(dispatcher),
            new PreviewChromeFactory(settings, new WindowIconService()),
            new DesktopBackdropService(),
            new FrameRateCounter());
        Controller = new MissionControlController(
            WindowEnumerator, MonitorService, overlay, new WindowActivationService(), settings, dispatcher);

        SingleInstanceService.Trace("Creating hotkey manager.");
        _hotkeyManager = new GlobalHotkeyManager();
        _hotkeyManager.Triggered += OnHotkeyPressed;
        string? hotkeyError = null;
        try { if (registerHotkey) _hotkeyManager.Register(settings); }
        catch (Win32Exception exception) { hotkeyError = exception.Message; }

        SingleInstanceService.Trace("Creating tray icon.");
        _trayIcon = new TrayIconService();
        _trayIcon.ToggleRequested += OnToggleRequested;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.EnabledToggleRequested += OnEnabledToggleRequested;
        _trayIcon.StartupToggleRequested += OnStartupToggleRequested;
        _trayIcon.ExitRequested += OnExitRequested;
        SingleInstanceService.Trace("Applying live settings.");
        ApplyLiveSettings();
        if (hotkeyError is not null)
        {
            _trayIcon.ShowWarning(hotkeyError);
        }
        SingleInstanceService.Trace("Host constructor complete.");
    }

    public MissionControlSettings Settings { get; }
    public MissionControlController Controller { get; }
    public WindowEnumerator WindowEnumerator { get; }
    public MonitorService MonitorService { get; }

    public void OpenSettings()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(OpenSettings);
            return;
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(Settings, SaveAsync, TryChangeHotkey, ApplyLiveSettings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
        _settingsWindow.Topmost = true;
        _settingsWindow.Topmost = false;
        _settingsWindow.Focus();
    }

    public void HandleCommand(string command)
    {
        _dispatcher.BeginInvoke(() =>
        {
            switch (command.Trim().ToLowerInvariant())
            {
                case "--toggle": Controller.Toggle(); break;
                case "--show": Controller.Show(); break;
                case "--hide": Controller.Hide(); break;
                case "--exit": _requestExit(); break;
                case "--settings":
                default: OpenSettings(); break;
            }
        });
    }

    public async Task SaveAsync() => await _settingsService.SaveAsync(Settings).ConfigureAwait(false);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settingsWindow?.Close();
        _hotkeyManager.Triggered -= OnHotkeyPressed;
        _hotkeyManager.Dispose();
        Controller.Dispose();
        _trayIcon.Dispose();
        try { _settingsService.SaveAsync(Settings).GetAwaiter().GetResult(); } catch (IOException) { }
    }

    private bool TryChangeHotkey(HotkeyModifiers modifiers, uint virtualKey)
    {
        var previousModifiers = Settings.Input.ShortcutModifiers;
        var previousVirtualKey = Settings.Input.ShortcutVirtualKey;
        try
        {
            _hotkeyManager.Register(modifiers, virtualKey);
            return true;
        }
        catch (Win32Exception)
        {
            try { _hotkeyManager.Register(previousModifiers, previousVirtualKey); } catch (Win32Exception) { }
            return false;
        }
    }

    private void ApplyLiveSettings()
    {
        SingleInstanceService.Trace("Updating tray state.");
        _trayIcon.Update(Settings.General.ShowTrayIcon, Settings.General.Enabled, Settings.General.StartWithWindows);
        SingleInstanceService.Trace("Tray state updated.");
        System.Windows.Media.RenderOptions.ProcessRenderMode = Settings.Advanced.HardwareAcceleration
            ? RenderMode.Default
            : RenderMode.SoftwareOnly;
        if (_manageStartupRegistration && _lastStartWithWindows != Settings.General.StartWithWindows)
        {
            _lastStartWithWindows = Settings.General.StartWithWindows;
            _ = UpdateStartupRegistrationAsync();
        }
        if (!Settings.General.Enabled) Controller.Hide();
    }

    private void OnHotkeyPressed(object? sender, EventArgs eventArgs) { if (Settings.General.Enabled) Controller.Toggle(); }
    private void OnToggleRequested(object? sender, EventArgs eventArgs) { if (Settings.General.Enabled) Controller.Toggle(); }
    private void OnSettingsRequested(object? sender, EventArgs eventArgs) => OpenSettings();
    private void OnEnabledToggleRequested(object? sender, EventArgs eventArgs) { Settings.General.Enabled = !Settings.General.Enabled; ApplyLiveSettings(); _ = SaveAsync(); }
    private void OnStartupToggleRequested(object? sender, EventArgs eventArgs) { Settings.General.StartWithWindows = !Settings.General.StartWithWindows; ApplyLiveSettings(); _ = SaveAsync(); }
    private void OnExitRequested(object? sender, EventArgs eventArgs) => _requestExit();

    private async Task UpdateStartupRegistrationAsync()
    {
        await _startupUpdateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            SingleInstanceService.Trace("Updating startup registration.");
            var enabled = Settings.General.StartWithWindows;
            await Task.Run(() =>
            {
                if (enabled) _startupService.RegisterCurrentExecutable();
                else _startupService.Unregister();
            }).ConfigureAwait(false);
            SingleInstanceService.Trace("Startup registration updated.");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            _ = _dispatcher.BeginInvoke(() => _trayIcon.ShowWarning("Windows startup registration could not be changed."));
        }
        finally
        {
            _startupUpdateLock.Release();
        }
    }
}

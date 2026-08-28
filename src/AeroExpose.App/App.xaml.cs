using System.Diagnostics;
using System.IO;
using System.Windows;
using AeroExpose.AppServices;
using AeroExpose.Core.Settings;
using AeroExpose.Native;
using AeroExpose.Services;
using AeroExpose.WindowManagement;

namespace AeroExpose;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;
    private BackgroundApplicationHost? _host;
    private string? _pendingCommand;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        var isSmokeTest = string.Equals(
            Environment.GetEnvironmentVariable("AEROEXPOSE_SMOKE_TEST"),
            "1",
            StringComparison.Ordinal);
        try
        {
            _singleInstance = new SingleInstanceService();
            if (!_singleInstance.IsPrimary)
            {
                var command = GetRequestedCommand(eventArgs.Args, defaultCommand: "--settings");
                await SingleInstanceService.SendAsync(command).ConfigureAwait(true);
                Shutdown();
                return;
            }
            _singleInstance.CommandReceived += OnCommandReceived;
            _singleInstance.StartListening();

            var settingsService = new SettingsService(Environment.GetEnvironmentVariable("AEROEXPOSE_SETTINGS_PATH"));
            var settings = await settingsService.LoadAsync().ConfigureAwait(true);
            SingleInstanceService.Trace("Settings loaded.");
            if (isSmokeTest)
            {
                settings.DebugOverlayEnabled = true;
                settings.ShowMinimizedWindows = true;
            }

            SingleInstanceService.Trace("Creating background host.");
            _host = new BackgroundApplicationHost(
                settings, settingsService, Dispatcher, () => Shutdown(),
                registerHotkey: !isSmokeTest,
                manageStartupRegistration: !isSmokeTest);
            SingleInstanceService.Trace("Background host ready.");
            var pendingCommand = Interlocked.Exchange(ref _pendingCommand, null);
            if (pendingCommand is not null)
            {
                _host.HandleCommand(pendingCommand);
            }

            if (isSmokeTest)
            {
                await RunSmokeTestAsync(_host.WindowEnumerator, _host.MonitorService, settings).ConfigureAwait(true);
            }
            else
            {
                var requested = GetRequestedCommand(eventArgs.Args, defaultCommand: string.Empty);
                if (!string.IsNullOrEmpty(requested) && requested != "--background")
                {
                    _host.HandleCommand(requested);
                }
                else if (requested != "--background" && settings.General.LaunchBehavior == LaunchBehavior.OpenSettings)
                {
                    _host.OpenSettings();
                }
            }
        }
        catch (Exception exception)
        {
            var resultPath = Environment.GetEnvironmentVariable("AEROEXPOSE_SMOKE_RESULT_PATH");
            if (isSmokeTest && !string.IsNullOrWhiteSpace(resultPath))
            {
                await File.WriteAllTextAsync(resultPath, exception.ToString()).ConfigureAwait(true);
            }
            else
            {
                MessageBox.Show(
                    exception.Message,
                    "AeroExpose could not start",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        if (_singleInstance is not null)
        {
            _singleInstance.CommandReceived -= OnCommandReceived;
        }
        _host?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(eventArgs);
    }

    private void OnCommandReceived(object? sender, string command)
    {
        if (_host is { } host) host.HandleCommand(command);
        else Interlocked.Exchange(ref _pendingCommand, command);
    }

    private static string GetRequestedCommand(IReadOnlyList<string> arguments, string defaultCommand)
    {
        var command = arguments.FirstOrDefault(argument => argument is "--settings" or "--toggle" or "--show" or "--hide" or "--exit" or "--background");
        return command ?? defaultCommand;
    }

    private async Task RunSmokeTestAsync(
        WindowEnumerator windowEnumerator,
        MonitorService monitorService,
        MissionControlSettings settings)
    {
        if (_host is null)
        {
            return;
        }

        var previouslyActiveWindow = NativeMethods.GetForegroundWindow();
        var monitor = monitorService.GetCursorMonitor();
        var allWindows = await Task.Run(windowEnumerator.Enumerate).ConfigureAwait(true);
        var sessionWindows = settings.MonitorMode switch
        {
            MonitorMode.AllWindowsOnCursorMonitor => allWindows,
            _ => allWindows.Where(window => window.MonitorHandle == monitor.Handle).ToArray(),
        };
        var target = sessionWindows
            .Where(window => window.Handle != previouslyActiveWindow)
            .OrderBy(window => window.IsMinimized)
            .FirstOrDefault();
        if (target is not null && string.Equals(
            Environment.GetEnvironmentVariable("AEROEXPOSE_SMOKE_MINIMIZE_TARGET"),
            "1",
            StringComparison.Ordinal))
        {
            NativeMethods.ShowWindow(target.Handle, NativeMethods.ShowMinimize);
            await Task.Delay(100).ConfigureAwait(true);
        }
        var trace = new List<string>();
        var traceSync = new object();
        var stopwatch = Stopwatch.StartNew();
        using var traceCancellation = new CancellationTokenSource();
        var traceTask = Task.Run(async () =>
        {
            var lastForeground = new nint(-1);
            try
            {
                while (!traceCancellation.IsCancellationRequested)
                {
                    var foreground = NativeMethods.GetForegroundWindow();
                    if (foreground != lastForeground)
                    {
                        NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
                        lock (traceSync)
                        {
                            trace.Add($"{stopwatch.Elapsed.TotalMilliseconds,8:F1} ms  " +
                                $"foreground=0x{foreground.ToInt64():X} pid={processId}");
                        }

                        lastForeground = foreground;
                    }

                    await Task.Delay(5, traceCancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (traceCancellation.IsCancellationRequested)
            {
                // The foreground trace ends with the smoke test.
            }
        });

        var exitCode = 0;
        await _host.Controller.ShowAsync().ConfigureAwait(true);
        await Task.Delay(500).ConfigureAwait(true);
        if (target is not null && NativeMethods.IsWindow(target.Handle))
        {
            await _host.Controller.SelectAsync(target.Handle).ConfigureAwait(true);
            var activationDeadline = Stopwatch.StartNew();
            while (NativeMethods.GetForegroundWindow() != target.Handle &&
                activationDeadline.Elapsed < TimeSpan.FromMilliseconds(750))
            {
                await Task.Delay(10).ConfigureAwait(true);
            }

            if (NativeMethods.GetForegroundWindow() == target.Handle)
            {
                var stabilityDeadline = Stopwatch.StartNew();
                while (stabilityDeadline.Elapsed < TimeSpan.FromMilliseconds(1500))
                {
                    await Task.Delay(10).ConfigureAwait(true);
                    if (NativeMethods.GetForegroundWindow() != target.Handle)
                    {
                        exitCode = 2;
                        break;
                    }
                }
            }
            else
            {
                exitCode = 2;
            }
        }
        else
        {
            await _host.Controller.HideAsync().ConfigureAwait(true);
            exitCode = 3;
        }

        traceCancellation.Cancel();
        await traceTask.ConfigureAwait(true);
        var finalForeground = NativeMethods.GetForegroundWindow();
        if (target is not null && finalForeground != target.Handle)
        {
            exitCode = 2;
        }
        var resultPath = Environment.GetEnvironmentVariable("AEROEXPOSE_SMOKE_RESULT_PATH");
        if (!string.IsNullOrWhiteSpace(resultPath))
        {
            string[] transitionLines;
            lock (traceSync)
            {
                transitionLines = trace.ToArray();
            }

            await File.WriteAllLinesAsync(resultPath,
            [
                $"previous=0x{previouslyActiveWindow.ToInt64():X}",
                $"target=0x{target?.Handle.ToInt64() ?? 0:X}",
                $"targetTitle={target?.Title ?? "<none>"}",
                $"targetProcess={target?.ProcessName ?? "<none>"}",
                $"final=0x{finalForeground.ToInt64():X}",
                $"exitCode={exitCode}",
                "transitions:",
                .. transitionLines,
            ]).ConfigureAwait(true);
        }

        Shutdown(exitCode);
    }
}

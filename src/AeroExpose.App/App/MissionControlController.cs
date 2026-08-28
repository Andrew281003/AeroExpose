using System.Diagnostics;
using System.Windows.Threading;
using AeroExpose.Core.Models;
using AeroExpose.Core.Settings;
using AeroExpose.UI;
using AeroExpose.WindowManagement;

namespace AeroExpose.AppServices;

/// <summary>Coordinates input, window discovery, monitor policy, and the current overlay session.</summary>
public sealed class MissionControlController : IDisposable
{
    private readonly WindowEnumerator _windowEnumerator;
    private readonly MonitorService _monitorService;
    private readonly IMissionControlOverlay _overlay;
    private readonly WindowActivationService _activationService;
    private readonly MissionControlSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private CancellationTokenSource _transitionCancellation = new();
    private bool _disposed;

    internal MissionControlController(
        WindowEnumerator windowEnumerator,
        MonitorService monitorService,
        IMissionControlOverlay overlay,
        WindowActivationService activationService,
        MissionControlSettings settings,
        Dispatcher dispatcher)
    {
        _windowEnumerator = windowEnumerator;
        _monitorService = monitorService;
        _overlay = overlay;
        _activationService = activationService;
        _settings = settings;
        _dispatcher = dispatcher;
        _overlay.DismissRequested += OnDismissRequested;
        _overlay.WindowSelectionRequested += OnWindowSelectionRequested;
    }

    /// <summary>Toggles the Mission Control overview. Safe to call from gesture integrations.</summary>
    public void Toggle() => Schedule(ToggleAsync);

    /// <summary>Shows the overview. Intended as the future four-finger-swipe integration point.</summary>
    public void Show() => Schedule(ShowAsync);

    public void Hide() => Schedule(HideAsync);

    internal async Task ToggleAsync()
    {
        if (!_settings.General.Enabled && !_overlay.IsOverviewVisible)
        {
            return;
        }

        if (_overlay.IsOverviewVisible)
        {
            await HideAsync().ConfigureAwait(true);
        }
        else
        {
            await ShowAsync().ConfigureAwait(true);
        }
    }

    internal async Task ShowAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_settings.General.Enabled)
        {
            return;
        }

        await RunTransitionAsync(async cancellationToken =>
        {
            var previouslyActiveWindow = Native.NativeMethods.GetForegroundWindow();
            var discovery = await Task.Run(() =>
            {
                var monitor = _monitorService.GetCursorMonitor();
                var windows = _windowEnumerator.Enumerate();
                return (Monitor: monitor, Windows: windows);
            }, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            var monitor = discovery.Monitor;
            var allWindows = discovery.Windows;
            var windows = _settings.MonitorMode switch
            {
                MonitorMode.AllWindowsOnCursorMonitor => allWindows,
                _ => allWindows.Where(window => window.MonitorHandle == monitor.Handle).ToArray(),
            };

            var session = new MissionControlSession(
                monitor,
                windows,
                previouslyActiveWindow);
            await _overlay.ShowOverviewAsync(session, cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    internal Task HideAsync() => RunTransitionAsync(
        cancellationToken => _overlay.HideOverviewAsync(null, cancellationToken));

    internal Task SelectAsync(nint window) => RunTransitionAsync(async cancellationToken =>
    {
        await _overlay.HideOverviewAsync(window, cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        _activationService.TryActivate(window);
    });

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _overlay.DismissRequested -= OnDismissRequested;
        _overlay.WindowSelectionRequested -= OnWindowSelectionRequested;
        _transitionCancellation.Cancel();
        _transitionCancellation.Dispose();
        _transitionLock.Dispose();
        _overlay.Dispose();
    }

    private async Task RunTransitionAsync(Func<CancellationToken, Task> transition)
    {
        var nextCancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref _transitionCancellation, nextCancellation);
        previousCancellation.Cancel();
        previousCancellation.Dispose();

        await _transitionLock.WaitAsync().ConfigureAwait(true);
        try
        {
            await transition(nextCancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (nextCancellation.IsCancellationRequested)
        {
            // A newer toggle owns the visible state.
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    private Task InvokeOnUiAsync(Func<Task> action) =>
        _dispatcher.CheckAccess() ? action() : _dispatcher.InvokeAsync(action).Task.Unwrap();

    private void Schedule(Func<Task> action) => _ = RunSafelyAsync(action);

    private async Task RunSafelyAsync(Func<Task> action)
    {
        try
        {
            await InvokeOnUiAsync(action).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) when (_disposed)
        {
            // Shutdown raced with an external trigger.
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"AeroExpose transition failed: {exception}");
            try
            {
                await InvokeOnUiAsync(() => _overlay.HideOverviewAsync(null, CancellationToken.None))
                    .ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                Debug.WriteLine($"AeroExpose cleanup failed: {cleanupException}");
            }
        }
    }

    private void OnDismissRequested(object? sender, EventArgs eventArgs) => Hide();

    private void OnWindowSelectionRequested(object? sender, WindowSelectionRequestedEventArgs eventArgs) =>
        Schedule(() => SelectAsync(eventArgs.WindowHandle));
}

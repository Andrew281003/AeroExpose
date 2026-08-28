using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AeroExpose.Animation;
using AeroExpose.Core.Layout;
using AeroExpose.Core.Models;
using AeroExpose.Core.Settings;
using AeroExpose.Core.Utilities;
using AeroExpose.Diagnostics;
using AeroExpose.Native;
using AeroExpose.Rendering;
using AeroExpose.WindowManagement;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace AeroExpose.UI;

public partial class MissionControlOverlay : Window, IMissionControlOverlay
{
    private readonly DwmThumbnailManager _thumbnailManager = new();
    private readonly MissionControlSettings _settings;
    private readonly MissionControlLayoutEngine _layoutEngine;
    private readonly AnimationService _animationService;
    private readonly PreviewChromeFactory _chromeFactory;
    private readonly DesktopBackdropService _backdropService;
    private readonly FrameRateCounter _frameRateCounter;
    private readonly List<PreviewVisualState> _previews = [];
    private nint _windowHandle;
    private MissionControlSession? _session;
    private HwndSource? _hwndSource;
    private PreviewVisualState? _selectedPreview;
    private string _debugDescription = string.Empty;
    private bool _isTransitioning;
    private bool _disposed;

    internal MissionControlOverlay(
        MissionControlSettings settings,
        MissionControlLayoutEngine layoutEngine,
        AnimationService animationService,
        PreviewChromeFactory chromeFactory,
        DesktopBackdropService backdropService,
        FrameRateCounter frameRateCounter)
    {
        _settings = settings;
        _layoutEngine = layoutEngine;
        _animationService = animationService;
        _chromeFactory = chromeFactory;
        _backdropService = backdropService;
        _frameRateCounter = frameRateCounter;
        InitializeComponent();
        ApplyLiveSettings();
        DebugPanel.Visibility = _settings.DebugOverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
        HelpText.Visibility = _settings.DebugOverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
        SourceInitialized += OnSourceInitialized;
    }

    public bool IsOverviewVisible => IsVisible;

    public event EventHandler? DismissRequested;

    public event EventHandler<WindowSelectionRequestedEventArgs>? WindowSelectionRequested;

    public async Task ShowOverviewAsync(MissionControlSession session, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _isTransitioning = true;

        try
        {
            if (!IsVisible)
            {
                Show();
            }

            ApplyLiveSettings();
            PositionOnMonitor(session.Monitor);
            _backdropService.Apply(_windowHandle, _settings.BlurEnabled);
            BackdropTint.Opacity = 0d;
            _session = session;
            CreatePreviews(session);
            if (_settings.DebugOverlayEnabled)
            {
                _frameRateCounter.Start(fps => StatusText.Text = $"{_debugDescription}\nFPS {fps:F1}");
            }
            Activate();
            Focus();
            Keyboard.Focus(this);

            if (_previews.Count == 0)
            {
                BackdropTint.Opacity = 1d;
                return;
            }

            if (!_settings.Animations.Enabled)
            {
                BackdropTint.Opacity = 1d;
                foreach (var preview in _previews)
                {
                    ApplyPreviewState(preview, preview.TargetBounds, 1d);
                }
                return;
            }

            await _animationService.AnimateAsync(
                TimeSpan.FromMilliseconds(_settings.Animations.ReduceMotion ? 140 : _settings.Animations.EffectiveOpenDurationMs),
                progress =>
                {
                    BackdropTint.Opacity = progress;
                    for (var index = 0; index < _previews.Count; index++)
                    {
                        var preview = _previews[index];
                        var previewProgress = _settings.Animations.Stagger && !_settings.Animations.ReduceMotion
                            ? Math.Clamp((progress - (index * 0.018d)) / Math.Max(0.1d, 1d - (index * 0.018d)), 0d, 1d)
                            : progress;
                        var bounds = LayoutRect.Lerp(preview.OriginBounds, preview.TargetBounds, progress);
                        if (_settings.Animations.ReduceMotion)
                        {
                            bounds = preview.TargetBounds;
                        }
                        else
                        {
                            bounds = LayoutRect.Lerp(preview.OriginBounds, preview.TargetBounds, previewProgress);
                        }
                        ApplyPreviewState(preview, bounds, previewProgress);
                    }
                },
                EasingFunctions.ForStyle(_settings.Animations.ReduceMotion ? AnimationStyle.Smooth : _settings.Animations.Style),
                cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    public async Task HideOverviewAsync(nint? selectedWindow, CancellationToken cancellationToken)
    {
        if (!IsVisible)
        {
            return;
        }

        _isTransitioning = true;
        CancelHoverAnimations();
        try
        {
            var startingBackdropOpacity = BackdropTint.Opacity;
            var selected = selectedWindow is null
                ? null
                : _previews.FirstOrDefault(preview => preview.Window.Handle == selectedWindow.Value);
            var startingStates = _previews.Select(preview => (Preview: preview, Bounds: preview.CurrentBounds)).ToArray();
            if (!_settings.Animations.Enabled)
            {
                ClearPreviews();
                _backdropService.Remove(_windowHandle);
                Hide();
                _session = null;
                return;
            }

            await _animationService.AnimateAsync(
                TimeSpan.FromMilliseconds(_settings.Animations.ReduceMotion ? 110 : _settings.Animations.EffectiveCloseDurationMs),
                progress =>
                {
                    BackdropTint.Opacity = startingBackdropOpacity * (1d - progress);
                    foreach (var state in startingStates)
                    {
                        var isSelected = ReferenceEquals(state.Preview, selected);
                        var destination = _settings.Animations.ReduceMotion
                            ? state.Bounds
                            : selectedWindow is null
                            ? state.Preview.OriginBounds
                            : isSelected
                                ? CreateSelectionDestination(state.Preview)
                                : state.Bounds.ScaleAboutCenter(0.88d);
                        var bounds = LayoutRect.Lerp(state.Bounds, destination, progress);
                        var opacity = isSelected ? 1d : 1d - progress;
                        ApplyPreviewState(state.Preview, bounds, opacity);
                    }
                },
                EasingFunctions.ForStyle(_settings.Animations.ReduceMotion ? AnimationStyle.Smooth : _settings.Animations.Style),
                cancellationToken).ConfigureAwait(true);

            ClearPreviews();
            _backdropService.Remove(_windowHandle);
            Hide();
            _session = null;
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearPreviews();
        _frameRateCounter.Dispose();
        _hwndSource?.RemoveHook(WindowProcedure);
        _thumbnailManager.Dispose();
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WindowProcedure);
        if (_hwndSource is { CompositionTarget: { } compositionTarget })
        {
            compositionTarget.BackgroundColor = Colors.Transparent;
        }
    }

    private nint WindowProcedure(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is NativeMethods.WindowMessageDisplayChange or NativeMethods.WindowMessageDpiChanged &&
            IsVisible &&
            !_isTransitioning)
        {
            Dispatcher.BeginInvoke(() => DismissRequested?.Invoke(this, EventArgs.Empty));
        }

        return nint.Zero;
    }

    private void PositionOnMonitor(MonitorInfo monitor)
    {
        if (_windowHandle == nint.Zero)
        {
            _windowHandle = new WindowInteropHelper(this).EnsureHandle();
        }

        NativeMethods.SetWindowPos(
            _windowHandle,
            NativeMethods.HwndTopmost,
            monitor.Bounds.Left,
            monitor.Bounds.Top,
            monitor.Bounds.Width,
            monitor.Bounds.Height,
            NativeMethods.SetWindowPositionShowWindow);
    }

    private void OnKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            eventArgs.Handled = true;
            DismissRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_isTransitioning || _previews.Count == 0)
        {
            return;
        }

        if (eventArgs.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            eventArgs.Handled = true;
            MoveSelection(eventArgs.Key);
        }
        else if (eventArgs.Key is Key.Enter or Key.Space && _selectedPreview is not null)
        {
            eventArgs.Handled = true;
            RequestSelection(_selectedPreview);
        }
    }

    private void CreatePreviews(MissionControlSession session)
    {
        ClearPreviews();
        if (session.Windows.Count == 0)
        {
            _debugDescription = $"Monitor {session.Monitor.DeviceName} · DPI {session.Monitor.Dpi}\nNo eligible windows";
            StatusText.Text = _debugDescription;
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        var layout = CreateLayout(session);
        var windowsByHandle = session.Windows.ToDictionary(window => window.Handle);
        var liveCount = 0;
        var debugLines = new List<string>
        {
            $"Monitor {session.Monitor.DeviceName} · DPI {session.Monitor.Dpi} · layout {layout.Score:F3} · rows {layout.RowCount}",
        };
        foreach (var placement in layout.Placements)
        {
            var window = windowsByHandle[placement.WindowHandle];
            var origin = CreateOriginBounds(window, placement.Bounds, session.Monitor);
            var chrome = _chromeFactory.Create(PreviewChromeCanvas, window, origin, session.Monitor.Dpi);
            var registration = _settings.Advanced.LiveDwmThumbnails
                ? _thumbnailManager.Register(_windowHandle, window, origin, 0)
                : null;
            if (registration is not null)
            {
                liveCount++;
            }

            var preview = new PreviewVisualState(
                window,
                origin,
                placement.Bounds,
                chrome.Shell,
                chrome.Title,
                chrome.HitSurface,
                registration is not null,
                window.Handle == session.PreviouslyActiveWindow);
            chrome.HitSurface.Tag = preview;
            chrome.HitSurface.MouseEnter += OnPreviewMouseEnter;
            chrome.HitSurface.MouseLeave += OnPreviewMouseLeave;
            chrome.HitSurface.MouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            _previews.Add(preview);
            ApplyPreviewState(preview, origin, 0d);
            if (debugLines.Count < 18)
            {
                debugLines.Add(
                    $"0x{window.Handle.ToInt64():X} {window.ApplicationName} " +
                    $"src=[{window.Bounds.Left},{window.Bounds.Top},{window.Bounds.Width}x{window.Bounds.Height}] " +
                    $"dst=[{placement.Bounds.X:F0},{placement.Bounds.Y:F0},{placement.Bounds.Width:F0}x{placement.Bounds.Height:F0}] " +
                    $"dpi={window.Dpi} mon={window.MonitorDeviceName} dwm={(registration is null ? "fallback" : "live")}");
            }
        }

        SetSelectedPreview(
            _previews.FirstOrDefault(preview => preview.IsPreviouslyActive) ?? _previews.FirstOrDefault());
        debugLines.Insert(1, $"DWM thumbnails {liveCount}/{session.Windows.Count}");
        _debugDescription = string.Join(Environment.NewLine, debugLines);
        StatusText.Text = _debugDescription;
    }

    private void ApplyPreviewState(PreviewVisualState preview, LayoutRect physicalBounds, double opacity)
    {
        if (_session is null)
        {
            return;
        }

        preview.CurrentBounds = physicalBounds;
        preview.IsLive = preview.IsLive && _thumbnailManager.Update(
            preview.Window.Handle,
            physicalBounds,
            (byte)Math.Clamp(Math.Round(opacity * byte.MaxValue), 0d, byte.MaxValue),
            opacity > 0.001d);
        if (!preview.IsLive)
        {
            preview.Shell.Background = new SolidColorBrush(Color.FromRgb(35, 39, 48));
        }

        var rect = DpiMath.PixelsToDips(physicalBounds, _session.Monitor.Dpi);
        preview.Shell.Width = Math.Max(1d, rect.Width);
        preview.Shell.Height = Math.Max(1d, rect.Height);
        preview.Shell.Opacity = opacity;
        Canvas.SetLeft(preview.Shell, rect.X);
        Canvas.SetTop(preview.Shell, rect.Y);

        preview.HitSurface.Width = Math.Max(1d, rect.Width);
        preview.HitSurface.Height = Math.Max(1d, rect.Height);
        preview.HitSurface.Opacity = opacity <= 0.02d ? 0d : 1d;
        Canvas.SetLeft(preview.HitSurface, rect.X);
        Canvas.SetTop(preview.HitSurface, rect.Y);

        if (preview.Title is not null)
        {
            preview.Title.Width = Math.Max(1d, rect.Width);
            preview.Title.Opacity = opacity;
            Canvas.SetLeft(preview.Title, rect.X);
            Canvas.SetTop(preview.Title, rect.Bottom + 7);
        }
    }

    private LayoutResult CreateLayout(MissionControlSession session)
    {
        var monitor = session.Monitor;
        var localWorkArea = new LayoutRect(
            monitor.WorkArea.Left - monitor.Bounds.Left,
            monitor.WorkArea.Top - monitor.Bounds.Top,
            monitor.WorkArea.Width,
            monitor.WorkArea.Height);
        var items = session.Windows.Select(window =>
        {
            var bounds = window.Bounds.Offset(-monitor.Bounds.Left, -monitor.Bounds.Top);
            return new LayoutItem(window.Handle, window.Bounds.AspectRatio, bounds);
        }).ToArray();
        var gap = DpiMath.DipsToPixels(_settings.PreviewSpacing, monitor.Dpi);
        var titleHeight = _settings.ShowWindowTitles
            ? DpiMath.DipsToPixels(34d, monitor.Dpi)
            : 0d;
        var previewScale = _settings.Windows.PreviewScale;
        var options = new LayoutOptions(
            OuterMargin: Math.Max(gap * 1.75d, DpiMath.DipsToPixels(42d / previewScale, monitor.Dpi)),
            Gap: gap,
            TitleHeight: titleHeight,
            MinimumPreviewWidth: DpiMath.DipsToPixels(96d * previewScale, monitor.Dpi),
            MinimumPreviewHeight: DpiMath.DipsToPixels(64d * previewScale, monitor.Dpi));
        return _layoutEngine.Arrange(items, localWorkArea, options);
    }

    private static LayoutRect CreateOriginBounds(
        WindowInfo window,
        LayoutRect targetBounds,
        MonitorInfo monitor)
    {
        if (window.IsMinimized || window.Bounds.Left < -20_000 || window.Bounds.Top < -20_000)
        {
            var minimized = targetBounds.ScaleAboutCenter(0.62d);
            return new LayoutRect(
                minimized.X,
                monitor.WorkArea.Bottom - monitor.Bounds.Top - minimized.Height,
                minimized.Width,
                minimized.Height);
        }

        return DpiMath.DesktopPixelsToOverlayPixels(window.Bounds, monitor.Bounds);
    }

    private LayoutRect CreateSelectionDestination(PreviewVisualState preview)
    {
        if (_session is null)
        {
            return preview.OriginBounds;
        }

        var desktopBounds = preview.Window.IsMinimized
            ? preview.Window.RestoreBounds
            : preview.Window.Bounds;
        return DpiMath.DesktopPixelsToOverlayPixels(desktopBounds, _session.Monitor.Bounds);
    }

    private void MoveSelection(Key direction)
    {
        var current = _selectedPreview ?? _previews[0];
        var next = PreviewNavigationService.FindNext(_previews, current, direction);
        if (next is not null)
        {
            SetSelectedPreview(next);
        }
    }

    private void SetSelectedPreview(PreviewVisualState? selected)
    {
        _selectedPreview = selected;
        foreach (var preview in _previews)
        {
            preview.Shell.BorderBrush = ReferenceEquals(preview, selected)
                ? PreviewChromeFactory.SelectedBorderBrush
                : preview.IsPreviouslyActive
                    ? PreviewChromeFactory.ActiveBorderBrush
                    : PreviewChromeFactory.NormalBorderBrush;
            preview.Shell.BorderThickness = ReferenceEquals(preview, selected)
                ? new Thickness(2)
                : new Thickness(1);
        }
    }

    private async Task AnimateHoverAsync(PreviewVisualState preview, bool hovered)
    {
        if (_isTransitioning || !IsVisible)
        {
            return;
        }

        preview.HoverCancellation?.Cancel();
        preview.HoverCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        preview.HoverCancellation = cancellation;
        var start = preview.CurrentBounds;
        var shouldScale = _settings.Appearance.HoverEffect == HoverEffect.Scale;
        var destination = hovered && shouldScale
            ? preview.TargetBounds.ScaleAboutCenter(_settings.HoverScale)
            : preview.TargetBounds;

        try
        {
            await _animationService.AnimateAsync(
                TimeSpan.FromMilliseconds(_settings.HoverAnimationDurationMilliseconds),
                progress => ApplyPreviewState(preview, LayoutRect.Lerp(start, destination, progress), 1d),
                EasingFunctions.CubicOut,
                cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Pointer moved again before the small hover transition finished.
        }
    }

    private void OnPreviewMouseEnter(object sender, MouseEventArgs eventArgs)
    {
        if (sender is Border { Tag: PreviewVisualState preview })
        {
            SetSelectedPreview(preview);
            _ = AnimateHoverAsync(preview, true);
        }
    }

    private void OnPreviewMouseLeave(object sender, MouseEventArgs eventArgs)
    {
        if (sender is Border { Tag: PreviewVisualState preview })
        {
            _ = AnimateHoverAsync(preview, false);
        }
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!_isTransitioning && sender is Border { Tag: PreviewVisualState preview })
        {
            eventArgs.Handled = true;
            RequestSelection(preview);
        }
    }

    private void RequestSelection(PreviewVisualState preview)
    {
        if (!NativeMethods.IsWindow(preview.Window.Handle))
        {
            _thumbnailManager.Remove(preview.Window.Handle);
            preview.HitSurface.IsHitTestVisible = false;
            preview.Shell.Opacity = 0.35d;
            return;
        }

        WindowSelectionRequested?.Invoke(
            this,
            new WindowSelectionRequestedEventArgs(preview.Window.Handle));
    }

    private void CancelHoverAnimations()
    {
        foreach (var preview in _previews)
        {
            preview.HoverCancellation?.Cancel();
        }
    }

    private void ClearPreviews()
    {
        _frameRateCounter.Stop();
        foreach (var preview in _previews)
        {
            preview.HoverCancellation?.Cancel();
            preview.HoverCancellation?.Dispose();
            preview.HitSurface.MouseEnter -= OnPreviewMouseEnter;
            preview.HitSurface.MouseLeave -= OnPreviewMouseLeave;
            preview.HitSurface.MouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        }

        _thumbnailManager.Clear();
        _chromeFactory.ClearCache();
        _selectedPreview = null;
        _debugDescription = string.Empty;
        _previews.Clear();
        EmptyState.Visibility = Visibility.Collapsed;
        PreviewChromeCanvas.Children.Clear();
    }

    private void ApplyLiveSettings()
    {
        var effect = _settings.Appearance.BackgroundEffect;
        var alpha = effect is BackgroundEffect.Dim or BackgroundEffect.BlurAndDim
            ? (byte)Math.Round(_settings.Appearance.DimStrength * byte.MaxValue)
            : (byte)0;
        BackdropTint.Background = new SolidColorBrush(Color.FromArgb(alpha, 7, 9, 14));
        DebugPanel.Visibility = _settings.DebugOverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
        HelpText.Visibility = _settings.DebugOverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

}

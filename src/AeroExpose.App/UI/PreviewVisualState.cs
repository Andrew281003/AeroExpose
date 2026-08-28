using System.Windows;
using System.Windows.Controls;
using AeroExpose.Core.Models;

namespace AeroExpose.UI;

internal sealed class PreviewVisualState
{
    public PreviewVisualState(
        WindowInfo window,
        LayoutRect originBounds,
        LayoutRect targetBounds,
        Border shell,
        FrameworkElement? title,
        Border hitSurface,
        bool isLive,
        bool isPreviouslyActive)
    {
        Window = window;
        OriginBounds = originBounds;
        TargetBounds = targetBounds;
        CurrentBounds = originBounds;
        Shell = shell;
        Title = title;
        HitSurface = hitSurface;
        IsLive = isLive;
        IsPreviouslyActive = isPreviouslyActive;
    }

    public WindowInfo Window { get; }

    public LayoutRect OriginBounds { get; }

    public LayoutRect TargetBounds { get; }

    public LayoutRect CurrentBounds { get; set; }

    public Border Shell { get; }

    public FrameworkElement? Title { get; }

    public Border HitSurface { get; }

    public bool IsLive { get; set; }

    public bool IsPreviouslyActive { get; }

    public CancellationTokenSource? HoverCancellation { get; set; }
}

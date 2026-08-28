using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AeroExpose.Core.Models;
using AeroExpose.Core.Settings;
using AeroExpose.Core.Utilities;
using AeroExpose.WindowManagement;
using Brush = System.Windows.Media.Brush;

namespace AeroExpose.UI;

internal sealed class PreviewChromeFactory
{
    internal static readonly Brush NormalBorderBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)));
    internal static readonly Brush ActiveBorderBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(155, 120, 190, 255)));
    internal static readonly Brush SelectedBorderBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(225, 210, 235, 255)));

    private readonly MissionControlSettings _settings;
    private readonly WindowIconService _iconService;

    public PreviewChromeFactory(MissionControlSettings settings, WindowIconService iconService)
    {
        _settings = settings;
        _iconService = iconService;
    }

    public PreviewChrome Create(Canvas canvas, WindowInfo window, LayoutRect physicalRect, uint dpi)
    {
        var rect = DpiMath.PixelsToDips(physicalRect, dpi);
        var shell = CreateShell(rect);
        canvas.Children.Add(shell);

        var title = _settings.ShowWindowTitles
            ? CreateTitle(window, rect)
            : null;
        if (title is not null)
        {
            canvas.Children.Add(title);
        }

        var hitSurface = CreateHitSurface(rect);
        canvas.Children.Add(hitSurface);
        return new PreviewChrome(shell, title, hitSurface);
    }

    public void ClearCache() => _iconService.Clear();

    private Border CreateShell(LayoutRect rect)
    {
        var shell = new Border
        {
            Width = Math.Max(1d, rect.Width),
            Height = Math.Max(1d, rect.Height),
            CornerRadius = _settings.Appearance.PreviewCorners == PreviewCorners.Square
                ? new CornerRadius(0)
                : new CornerRadius(10),
            BorderBrush = NormalBorderBrush,
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            Effect = _settings.Appearance.PreviewShadow ? new DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 8,
                Opacity = 0.42,
                Color = Colors.Black,
            } : null,
            IsHitTestVisible = false,
            Opacity = 0d,
        };
        Canvas.SetLeft(shell, rect.X);
        Canvas.SetTop(shell, rect.Y);
        return shell;
    }

    private Border CreateTitle(WindowInfo window, LayoutRect rect)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var icon = _settings.Appearance.ShowIcons && _settings.Windows.ShowIcons
            ? _iconService.GetIcon(window.Handle)
            : null;
        if (icon is not null)
        {
            content.Children.Add(new Image
            {
                Source = icon,
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 7, 0),
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = window.Title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Freeze(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255))),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var title = new Border
        {
            Width = Math.Max(1d, rect.Width),
            Height = 28,
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = new CornerRadius(10),
            Background = Freeze(new SolidColorBrush(Color.FromArgb(72, 8, 10, 15))),
            Child = content,
            IsHitTestVisible = false,
            Opacity = 0d,
        };
        Canvas.SetLeft(title, rect.X);
        Canvas.SetTop(title, rect.Bottom + 7);
        return title;
    }

    private static Border CreateHitSurface(LayoutRect rect)
    {
        var hitSurface = new Border
        {
            Width = Math.Max(1d, rect.Width),
            Height = Math.Max(1d, rect.Height),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Opacity = 0d,
        };
        Canvas.SetLeft(hitSurface, rect.X);
        Canvas.SetTop(hitSurface, rect.Y);
        Panel.SetZIndex(hitSurface, 100);
        return hitSurface;
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

internal sealed record PreviewChrome(Border Shell, FrameworkElement? Title, Border HitSurface);

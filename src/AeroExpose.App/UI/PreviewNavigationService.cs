using System.Windows.Input;

namespace AeroExpose.UI;

internal static class PreviewNavigationService
{
    public static PreviewVisualState? FindNext(
        IReadOnlyList<PreviewVisualState> previews,
        PreviewVisualState current,
        Key direction) => previews
        .Where(preview => !ReferenceEquals(preview, current))
        .Select(preview => new NavigationCandidate(
            preview,
            preview.TargetBounds.CenterX - current.TargetBounds.CenterX,
            preview.TargetBounds.CenterY - current.TargetBounds.CenterY))
        .Where(candidate => direction switch
        {
            Key.Left => candidate.DeltaX < -1d,
            Key.Right => candidate.DeltaX > 1d,
            Key.Up => candidate.DeltaY < -1d,
            Key.Down => candidate.DeltaY > 1d,
            _ => false,
        })
        .OrderBy(candidate => direction is Key.Left or Key.Right
            ? Math.Abs(candidate.DeltaX) + (Math.Abs(candidate.DeltaY) * 0.55d)
            : Math.Abs(candidate.DeltaY) + (Math.Abs(candidate.DeltaX) * 0.55d))
        .Select(candidate => candidate.Preview)
        .FirstOrDefault();

    private sealed record NavigationCandidate(PreviewVisualState Preview, double DeltaX, double DeltaY);
}

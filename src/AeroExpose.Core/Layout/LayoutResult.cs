namespace AeroExpose.Core.Layout;

public sealed record LayoutResult(
    IReadOnlyList<LayoutPlacement> Placements,
    double Score,
    int RowCount,
    IReadOnlyList<int> RowSizes)
{
    public static LayoutResult Empty { get; } = new([], 0d, 0, []);
}

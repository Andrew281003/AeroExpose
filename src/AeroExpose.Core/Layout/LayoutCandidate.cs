namespace AeroExpose.Core.Layout;

public sealed record LayoutCandidate(
    IReadOnlyList<LayoutPlacement> Placements,
    IReadOnlyList<int> RowSizes,
    int RowCount,
    double Score,
    double AreaUtilization,
    double WidthBalance,
    double ScaleVariance);

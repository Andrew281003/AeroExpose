using AeroExpose.Core.Models;

namespace AeroExpose.Core.Layout;

/// <summary>
/// Produces a justified, aspect-preserving overview by scoring multiple row partitions.
/// The implementation is UI-independent and works entirely in physical pixels.
/// </summary>
public sealed class MissionControlLayoutEngine
{
    public LayoutResult Arrange(
        IReadOnlyList<LayoutItem> inputItems,
        LayoutRect availableArea,
        LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputItems);
        ArgumentNullException.ThrowIfNull(options);

        if (inputItems.Count == 0 || availableArea.IsEmpty)
        {
            return LayoutResult.Empty;
        }

        var items = inputItems
            .OrderBy(item => item.SourceBounds.Center.Y)
            .ThenBy(item => item.SourceBounds.Center.X)
            .ToArray();
        var contentArea = new LayoutRect(
            availableArea.X + options.OuterMargin,
            availableArea.Y + options.OuterMargin,
            Math.Max(1d, availableArea.Width - (options.OuterMargin * 2d)),
            Math.Max(1d, availableArea.Height - (options.OuterMargin * 2d)));

        var maxRows = Math.Min(items.Length, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(items.Length * 1.8d)) + 2));
        LayoutCandidate? best = null;
        for (var rowCount = 1; rowCount <= maxRows; rowCount++)
        {
            var candidate = BuildCandidate(items, contentArea, options, rowCount);
            if (candidate is not null && (best is null || candidate.Score > best.Score))
            {
                best = candidate;
            }
        }

        return best is null
            ? LayoutResult.Empty
            : new LayoutResult(best.Placements, best.Score, best.RowCount, best.RowSizes);
    }

    private static LayoutCandidate? BuildCandidate(
        IReadOnlyList<LayoutItem> items,
        LayoutRect area,
        LayoutOptions options,
        int rowCount)
    {
        var fixedVerticalSpace = ((rowCount - 1) * options.Gap) + (rowCount * options.TitleHeight);
        var previewHeightBudget = area.Height - fixedVerticalSpace;
        if (previewHeightBudget <= 0d)
        {
            return null;
        }

        var rowSizes = PartitionRows(items, rowCount, area.Width, previewHeightBudget);
        if (rowSizes.Count != rowCount)
        {
            return null;
        }

        var rows = new List<IReadOnlyList<LayoutItem>>(rowCount);
        var itemIndex = 0;
        foreach (var rowSize in rowSizes)
        {
            rows.Add(items.Skip(itemIndex).Take(rowSize).ToArray());
            itemIndex += rowSize;
        }

        var naturalHeights = rows
            .Select(row =>
            {
                var rowGaps = (row.Count - 1) * options.Gap;
                var widthForWindows = Math.Max(1d, area.Width - rowGaps);
                return widthForWindows / row.Sum(item => item.SafeAspectRatio);
            })
            .ToArray();
        var heightScale = Math.Min(1d, previewHeightBudget / naturalHeights.Sum());
        var rowHeights = naturalHeights.Select(height => height * heightScale).ToArray();
        var usedHeight = rowHeights.Sum() + fixedVerticalSpace;
        var y = area.Y + Math.Max(0d, (area.Height - usedHeight) / 2d);
        var placements = new List<LayoutPlacement>(items.Count);
        var rowWidths = new List<double>(rowCount);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowHeight = rowHeights[rowIndex];
            var previewWidths = row.Select(item => rowHeight * item.SafeAspectRatio).ToArray();
            var usedWidth = previewWidths.Sum() + ((row.Count - 1) * options.Gap);
            rowWidths.Add(usedWidth);
            var x = area.X + ((area.Width - usedWidth) / 2d);

            for (var column = 0; column < row.Count; column++)
            {
                var item = row[column];
                var width = previewWidths[column];
                var bounds = new LayoutRect(x, y, width, rowHeight);
                var sourceArea = Math.Max(1d, (double)item.SourceBounds.Width * item.SourceBounds.Height);
                var scale = Math.Sqrt((width * rowHeight) / sourceArea);
                placements.Add(new LayoutPlacement(item.WindowHandle, bounds, rowIndex, scale));
                x += width + options.Gap;
            }

            y += rowHeight + options.TitleHeight + options.Gap;
        }

        var totalArea = placements.Sum(placement => placement.Bounds.Width * placement.Bounds.Height);
        var areaUtilization = totalArea / Math.Max(1d, area.Width * area.Height);
        var widthBalance = 1d - (rowWidths.Max() - rowWidths.Min()) / Math.Max(1d, area.Width);
        var scaleVariance = CalculateVariance(placements.Select(placement => Math.Log(Math.Max(0.001d, placement.ScaleFromSource))));
        var heightUtilization = usedHeight / area.Height;
        var undersizedCount = placements.Count(placement =>
            placement.Bounds.Width < options.MinimumPreviewWidth ||
            placement.Bounds.Height < options.MinimumPreviewHeight);
        var orphanPenalty = rowCount > 1 && rowSizes[^1] == 1 && items.Count > 3 ? 0.18d : 0d;
        var rowPenalty = rowCount * 0.012d;
        var undersizedPenalty = (double)undersizedCount / items.Count;
        var movementPenalty = CalculateMovementPenalty(placements, items, area);

        var score =
            (areaUtilization * 5.2d) +
            (Math.Clamp(widthBalance, 0d, 1d) * 0.65d) +
            (Math.Clamp(heightUtilization, 0d, 1d) * 0.55d) -
            (scaleVariance * 0.32d) -
            (undersizedPenalty * 1.4d) -
            orphanPenalty -
            rowPenalty -
            (movementPenalty * 0.08d);

        return new LayoutCandidate(
            placements,
            rowSizes,
            rowCount,
            score,
            areaUtilization,
            widthBalance,
            scaleVariance);
    }

    private static IReadOnlyList<int> PartitionRows(
        IReadOnlyList<LayoutItem> items,
        int rowCount,
        double availableWidth,
        double previewHeightBudget)
    {
        var count = items.Count;
        var idealRowHeight = previewHeightBudget / rowCount;
        var targetAspectSum = availableWidth / Math.Max(1d, idealRowHeight);
        var idealCount = (double)count / rowCount;
        var prefixAspects = new double[count + 1];
        for (var index = 0; index < count; index++)
        {
            prefixAspects[index + 1] = prefixAspects[index] + items[index].SafeAspectRatio;
        }

        var costs = new double[rowCount + 1, count + 1];
        var previous = new int[rowCount + 1, count + 1];
        for (var row = 0; row <= rowCount; row++)
        {
            for (var index = 0; index <= count; index++)
            {
                costs[row, index] = double.PositiveInfinity;
                previous[row, index] = -1;
            }
        }

        costs[0, 0] = 0d;
        for (var row = 1; row <= rowCount; row++)
        {
            var minimumEnd = row;
            var maximumEnd = count - (rowCount - row);
            for (var end = minimumEnd; end <= maximumEnd; end++)
            {
                for (var start = row - 1; start < end; start++)
                {
                    if (double.IsPositiveInfinity(costs[row - 1, start]))
                    {
                        continue;
                    }

                    var aspectSum = prefixAspects[end] - prefixAspects[start];
                    var segmentCount = end - start;
                    var aspectDelta = (aspectSum - targetAspectSum) / Math.Max(1d, targetAspectSum);
                    var countDelta = (segmentCount - idealCount) / Math.Max(1d, idealCount);
                    var cost = costs[row - 1, start] +
                        (aspectDelta * aspectDelta) +
                        (countDelta * countDelta * 0.08d);
                    if (cost < costs[row, end])
                    {
                        costs[row, end] = cost;
                        previous[row, end] = start;
                    }
                }
            }
        }

        if (previous[rowCount, count] < 0)
        {
            return [];
        }

        var sizes = new int[rowCount];
        var cursor = count;
        for (var row = rowCount; row > 0; row--)
        {
            var start = previous[row, cursor];
            sizes[row - 1] = cursor - start;
            cursor = start;
        }

        return sizes;
    }

    private static double CalculateVariance(IEnumerable<double> values)
    {
        var array = values.ToArray();
        if (array.Length <= 1)
        {
            return 0d;
        }

        var average = array.Average();
        return array.Sum(value => (value - average) * (value - average)) / array.Length;
    }

    private static double CalculateMovementPenalty(
        IReadOnlyList<LayoutPlacement> placements,
        IReadOnlyList<LayoutItem> items,
        LayoutRect area)
    {
        var diagonal = Math.Sqrt((area.Width * area.Width) + (area.Height * area.Height));
        if (diagonal <= 0d)
        {
            return 0d;
        }

        var sourceByHandle = items.ToDictionary(item => item.WindowHandle);
        return placements.Average(placement =>
        {
            var source = sourceByHandle[placement.WindowHandle].SourceBounds.Center;
            var deltaX = placement.Bounds.CenterX - source.X;
            var deltaY = placement.Bounds.CenterY - source.Y;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) / diagonal;
        });
    }
}

namespace AeroExpose.Core.Layout;

public sealed record LayoutOptions(
    double OuterMargin,
    double Gap,
    double TitleHeight,
    double MinimumPreviewWidth = 96d,
    double MinimumPreviewHeight = 64d);

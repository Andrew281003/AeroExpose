namespace AeroExpose.Core.Models;

/// <summary>A layout rectangle in overlay-local physical pixels.</summary>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + (Width / 2d);

    public double CenterY => Y + (Height / 2d);

    public bool IsEmpty => Width <= 0d || Height <= 0d;

    public LayoutRect ScaleAboutCenter(double scale)
    {
        var width = Width * scale;
        var height = Height * scale;
        return new LayoutRect(CenterX - (width / 2d), CenterY - (height / 2d), width, height);
    }

    public static LayoutRect Lerp(LayoutRect from, LayoutRect to, double progress) => new(
        from.X + ((to.X - from.X) * progress),
        from.Y + ((to.Y - from.Y) * progress),
        from.Width + ((to.Width - from.Width) * progress),
        from.Height + ((to.Height - from.Height) * progress));
}

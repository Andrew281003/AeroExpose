namespace AeroExpose.Core.Models;

/// <summary>A rectangle expressed in physical desktop pixels.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);

    public int Height => Math.Max(0, Bottom - Top);

    public double AspectRatio => Height == 0 ? 1d : (double)Width / Height;

    public bool IsEmpty => Width == 0 || Height == 0;

    public PixelPoint Center => new(Left + (Width / 2), Top + (Height / 2));

    public PixelRect Offset(int x, int y) => new(Left + x, Top + y, Right + x, Bottom + y);

    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top ? default : new PixelRect(left, top, right, bottom);
    }
}

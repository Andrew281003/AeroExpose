namespace AeroExpose.Animation;

using AeroExpose.Core.Settings;

internal static class EasingFunctions
{
    public static double PremiumOut(double progress)
    {
        if (progress >= 1d)
        {
            return 1d;
        }

        var cubic = 1d - Math.Pow(1d - progress, 3d);
        var settle = Math.Sin(progress * Math.PI) * Math.Exp(-5.2d * progress) * 0.055d;
        return cubic + settle;
    }

    public static double CubicInOut(double progress) => progress < 0.5d
        ? 4d * progress * progress * progress
        : 1d - (Math.Pow((-2d * progress) + 2d, 3d) / 2d);

    public static double CubicOut(double progress) => 1d - Math.Pow(1d - progress, 3d);

    public static Func<double, double> ForStyle(AnimationStyle style) => style switch
    {
        AnimationStyle.Spring => PremiumOut,
        AnimationStyle.Snappy => CubicOut,
        AnimationStyle.Linear => static progress => progress,
        _ => CubicInOut,
    };
}

namespace RelayBoxMatcher.Core.Imaging;

/// <summary>H in gradi [0,360), S e V in [0,1].</summary>
public readonly record struct HsvColor(double H, double S, double V);

public static class ColorConversion
{
    public static HsvColor ToHsv(byte r, byte g, byte b)
    {
        double rn = r / 255.0, gn = g / 255.0, bn = b / 255.0;
        double max = Math.Max(rn, Math.Max(gn, bn));
        double min = Math.Min(rn, Math.Min(gn, bn));
        double delta = max - min;

        double h = 0;
        if (delta > 1e-9)
        {
            if (max == rn) h = 60 * (((gn - bn) / delta) % 6);
            else if (max == gn) h = 60 * (((bn - rn) / delta) + 2);
            else h = 60 * (((rn - gn) / delta) + 4);
        }
        if (h < 0) h += 360;

        double s = max <= 1e-9 ? 0 : delta / max;
        double v = max;

        return new HsvColor(h, s, v);
    }

    /// <summary>Distanza circolare tra due tonalità, normalizzata in [0,180].</summary>
    public static double HueDistance(double h1, double h2)
    {
        double d = Math.Abs(h1 - h2) % 360;
        return d > 180 ? 360 - d : d;
    }
}

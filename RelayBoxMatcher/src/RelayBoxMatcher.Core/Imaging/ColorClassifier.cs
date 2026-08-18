using RelayBoxMatcher.Core.Models;

namespace RelayBoxMatcher.Core.Imaging;

public class ColorPrototype
{
    public ColorClass ColorClass { get; set; }
    public double Hue { get; set; }
    public double Saturation { get; set; }
}

/// <summary>
/// I prototipi di colore (uno per classe: Blu/Rosa/Verde) si costruiscono campionando i ritagli del
/// campione stesso, non da soglie fisse hard-coded: ogni set di template si autocalibra sui colori reali
/// fotografati quel giorno con quella luce, invece di dipendere da una tabella di colori assoluta.
/// </summary>
public static class ColorClassifier
{
    public static ColorPrototype BuildPrototype(ColorClass colorClass, IEnumerable<ColorSampleResult> samples)
    {
        var list = samples.Where(s => s.ValidPixelCount > 0).ToList();
        if (list.Count == 0)
            return new ColorPrototype { ColorClass = colorClass, Hue = 0, Saturation = 0 };

        double sumSin = 0, sumCos = 0, sumS = 0;
        foreach (var s in list)
        {
            double rad = s.DominantHue * Math.PI / 180.0;
            sumSin += Math.Sin(rad);
            sumCos += Math.Cos(rad);
            sumS += s.AvgSaturation;
        }

        double hue = Math.Atan2(sumSin, sumCos) * 180.0 / Math.PI;
        if (hue < 0) hue += 360;

        return new ColorPrototype
        {
            ColorClass = colorClass,
            Hue = hue,
            Saturation = sumS / list.Count
        };
    }

    /// <summary>Distanza combinata tonalità+saturazione in [0,1] circa; più bassa = più simile.</summary>
    public static double Distance(ColorSampleResult sample, ColorPrototype proto)
    {
        double hueDist = ColorConversion.HueDistance(sample.DominantHue, proto.Hue) / 180.0;
        double satDist = Math.Abs(sample.AvgSaturation - proto.Saturation);
        return hueDist * 0.75 + satDist * 0.25;
    }

    public static (ColorClass best, double distance) Classify(ColorSampleResult sample, IReadOnlyList<ColorPrototype> prototypes)
    {
        ColorClass best = ColorClass.Sconosciuto;
        double bestDist = double.MaxValue;

        foreach (var proto in prototypes)
        {
            double dist = Distance(sample, proto);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = proto.ColorClass;
            }
        }

        return (best, bestDist);
    }
}

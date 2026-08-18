using System.Drawing;

namespace RelayBoxMatcher.Core.Imaging;

public class ColorSampleResult
{
    public double DominantHue { get; set; }
    public double AvgSaturation { get; set; }
    public double AvgValue { get; set; }
    public int ValidPixelCount { get; set; }
    public int GlarePixelCount { get; set; }
    public int DarkPixelCount { get; set; }
    public int TotalPixelCount { get; set; }

    public double ValidRatio => TotalPixelCount == 0 ? 0 : (double)ValidPixelCount / TotalPixelCount;
    public double DarkRatio => TotalPixelCount == 0 ? 0 : (double)DarkPixelCount / TotalPixelCount;
    public double GlareRatio => TotalPixelCount == 0 ? 0 : (double)GlarePixelCount / TotalPixelCount;
}

/// <summary>
/// Campiona il colore dominante di una regione scartando due categorie di pixel "rumore",
/// come discusso: il riflesso diretto del flash su plastica lucida (molto luminoso, poco saturo)
/// e la cavità nera dello zoccolo quando il relè non è montato (molto scuro).
/// </summary>
public static class ColorSampler
{
    private const double GlareMinValue = 0.85;
    private const double GlareMaxSaturation = 0.25;
    private const double DarkMaxValue = 0.14;

    /// <summary>Frazione di margine interno da escludere sui bordi della ROI, per non catturare
    /// pixel del connettore/plastica adiacente quando la proiezione via omografia non è pixel-perfect.</summary>
    private const double InsetMargin = 0.12;

    public static ColorSampleResult Sample(Bitmap bmp, Rectangle roi)
    {
        var inset = Inset(roi);
        inset = Rectangle.Intersect(inset, new Rectangle(0, 0, bmp.Width, bmp.Height));

        var result = new ColorSampleResult();
        if (inset.Width <= 0 || inset.Height <= 0) return result;

        var hueBins = new double[36]; // 10° per bin
        double sumS = 0, sumV = 0;
        int valid = 0, glare = 0, dark = 0, total = 0;

        for (int y = inset.Top; y < inset.Bottom; y++)
        {
            for (int x = inset.Left; x < inset.Right; x++)
            {
                var c = bmp.GetPixel(x, y);
                total++;

                var hsv = ColorConversion.ToHsv(c.R, c.G, c.B);

                if (hsv.V >= GlareMinValue && hsv.S <= GlareMaxSaturation)
                {
                    glare++;
                    continue;
                }
                if (hsv.V <= DarkMaxValue)
                {
                    dark++;
                    continue;
                }

                valid++;
                sumS += hsv.S;
                sumV += hsv.V;

                double weight = hsv.S * hsv.V;
                int bin = (int)(hsv.H / 10.0) % 36;
                hueBins[bin] += weight;
            }
        }

        result.TotalPixelCount = total;
        result.ValidPixelCount = valid;
        result.GlarePixelCount = glare;
        result.DarkPixelCount = dark;

        if (valid > 0)
        {
            result.AvgSaturation = sumS / valid;
            result.AvgValue = sumV / valid;
            result.DominantHue = WeightedCircularPeak(hueBins);
        }

        return result;
    }

    private static Rectangle Inset(Rectangle roi)
    {
        int dx = (int)(roi.Width * InsetMargin);
        int dy = (int)(roi.Height * InsetMargin);
        return Rectangle.Inflate(roi, -dx, -dy);
    }

    private static double WeightedCircularPeak(double[] hueBins)
    {
        int peakBin = 0;
        double peakVal = -1;
        for (int i = 0; i < hueBins.Length; i++)
        {
            if (hueBins[i] > peakVal) { peakVal = hueBins[i]; peakBin = i; }
        }
        if (peakVal <= 0) return 0;

        double sumSin = 0, sumCos = 0;
        for (int offset = -1; offset <= 1; offset++)
        {
            int bin = ((peakBin + offset) % hueBins.Length + hueBins.Length) % hueBins.Length;
            double centerDeg = bin * 10.0 + 5.0;
            double rad = centerDeg * Math.PI / 180.0;
            double w = hueBins[bin];
            sumSin += w * Math.Sin(rad);
            sumCos += w * Math.Cos(rad);
        }

        double angle = Math.Atan2(sumSin, sumCos) * 180.0 / Math.PI;
        if (angle < 0) angle += 360;
        return angle;
    }
}

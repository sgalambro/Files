using System.Drawing;
using RelayBoxMatcher.Core.Imaging;
using RelayBoxMatcher.Core.Models;

namespace RelayBoxMatcher.Core.Services;

public class SlotDetection
{
    public TemplateSlot Slot { get; set; } = null!;
    public Rectangle ProjectedRect { get; set; }
    public ColorSampleResult Sample { get; set; } = null!;
    public PresenceStatus Presence { get; set; }
    public ColorClass DetectedColor { get; set; }
    public double ColorDistance { get; set; } = 1.0;
}

public class MatchingResult
{
    public string TestImageFileName { get; set; } = "";
    public List<SlotDetection> Detections { get; set; } = new();
}

/// <summary>
/// Applica il modello (template) creato dal campione a un'immagine di test: calibra tramite omografia
/// usando i punti di riferimento cliccati dall'operatore, proietta ogni slot nella foto di test e ne
/// classifica presenza/colore con campionamento robusto al riflesso del flash.
/// </summary>
public static class MatchingService
{
    // Sotto questa frazione di pixel "validi" (non riflesso, non cavità nera) nella ROI, il colore
    // letto non è abbastanza affidabile da fidarsene ciecamente.
    private const double MinValidRatioForConfidentRead = 0.15;
    private const double MinValidRatioForCertain = 0.35;

    // Sopra questa frazione di pixel scuri la ROI è verosimilmente uno zoccolo vuoto (nessun relè montato).
    private const double DarkRatioForAbsent = 0.45;

    // Distanza colore oltre la quale non ci fidiamo della classificazione (probabile riflesso/angolazione anomala).
    private const double MaxColorDistanceForMatch = 0.30;

    public static List<ColorPrototype> BuildPrototypes(string templateFolder, RelayTemplate template)
    {
        var prototypes = new List<ColorPrototype>();
        foreach (var group in template.Templates.GroupBy(t => t.ColorClass))
        {
            if (group.Key == ColorClass.Sconosciuto) continue;

            var samples = new List<ColorSampleResult>();
            foreach (var slot in group)
            {
                using var crop = TemplateService.LoadSlotCrop(templateFolder, slot);
                samples.Add(ColorSampler.Sample(crop, new Rectangle(0, 0, crop.Width, crop.Height)));
            }
            prototypes.Add(ColorClassifier.BuildPrototype(group.Key, samples));
        }
        return prototypes;
    }

    public static MatchingResult Match(
        string templateFolder,
        RelayTemplate template,
        Bitmap testImage,
        IReadOnlyList<PointD> testCalibrationPoints,
        string testImageFileName)
    {
        if (template.ReferencePoints.Count < 4)
            throw new InvalidOperationException("Il template non ha punti di riferimento sufficienti (minimo 4).");
        if (testCalibrationPoints.Count != template.ReferencePoints.Count)
            throw new ArgumentException(
                $"Numero di punti di calibrazione sulla foto di test ({testCalibrationPoints.Count}) diverso da quello del campione ({template.ReferencePoints.Count}). Clicca gli stessi punti, nello stesso ordine.");

        var src = template.ReferencePoints.Select(p => new PointD(p.X, p.Y)).ToList();
        var homography = Homography.Fit(src, testCalibrationPoints);

        var prototypes = BuildPrototypes(templateFolder, template);

        var detections = new List<SlotDetection>();
        foreach (var slot in template.Templates.OrderBy(t => t.Index))
        {
            var projected = homography.TransformRect(slot.Bbox.ToRectangle());
            var rect = projected.ToRectangle(testImage.Width, testImage.Height);

            var sample = ColorSampler.Sample(testImage, rect);
            var (presence, detectedColor, colorDist) = Classify(slot, sample, prototypes);

            detections.Add(new SlotDetection
            {
                Slot = slot,
                ProjectedRect = rect,
                Sample = sample,
                Presence = presence,
                DetectedColor = detectedColor,
                ColorDistance = colorDist
            });
        }

        return new MatchingResult { TestImageFileName = testImageFileName, Detections = detections };
    }

    private static (PresenceStatus presence, ColorClass detected, double dist) Classify(
        TemplateSlot slot, ColorSampleResult sample, List<ColorPrototype> prototypes)
    {
        if (sample.ValidRatio < MinValidRatioForConfidentRead)
        {
            var presence = sample.DarkRatio > DarkRatioForAbsent ? PresenceStatus.Assente : PresenceStatus.Incerto;
            return (presence, ColorClass.Sconosciuto, 1.0);
        }

        var (best, dist) = ColorClassifier.Classify(sample, prototypes);

        if (sample.DarkRatio > DarkRatioForAbsent && sample.ValidRatio < MinValidRatioForCertain)
            return (PresenceStatus.Assente, best, dist);

        if (dist > MaxColorDistanceForMatch)
            return (PresenceStatus.Incerto, best, dist);

        if (best != slot.ColorClass)
            return (PresenceStatus.ColoreErrato, best, dist);

        return (PresenceStatus.Presente, best, dist);
    }
}

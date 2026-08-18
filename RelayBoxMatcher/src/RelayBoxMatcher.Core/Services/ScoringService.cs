using RelayBoxMatcher.Core.Models;

namespace RelayBoxMatcher.Core.Services;

/// <summary>
/// Confronta il risultato del match con una ground truth opzionale (expected.json) e produce il
/// TestReport nello stesso schema già usato in test_report.json. Se non c'è ground truth per
/// l'immagine, i risultati restano "TN/unlabeled" (stessa convenzione già presente nei report
/// esistenti) e Precision/Recall/F1 restano nulli: è la modalità "controllo in produzione senza
/// riferimento", utile quando si vuole solo vedere lo stato rilevato senza etichettare a mano.
/// </summary>
public static class ScoringService
{
    public const double DefaultPassThreshold = 0.85;

    public static TestReport Build(MatchingResult matching, ExpectedImage? expected, double passThreshold = DefaultPassThreshold)
    {
        var report = new TestReport { TestImage = matching.TestImageFileName };

        int tp = 0, fp = 0, fn = 0;
        int wrongColorExpected = 0, wrongColorMismatch = 0;

        foreach (var d in matching.Detections)
        {
            var exp = expected?.Expected.FirstOrDefault(e =>
                string.Equals(e.Template, d.Slot.Name, StringComparison.OrdinalIgnoreCase));

            bool detectedPresent = d.Presence is PresenceStatus.Presente or PresenceStatus.ColoreErrato;
            bool wrongColor = d.Presence == PresenceStatus.ColoreErrato;

            string status;
            if (exp == null)
            {
                status = "TN";
            }
            else if (exp.Present)
            {
                wrongColorExpected++;
                if (wrongColor) wrongColorMismatch++;

                if (detectedPresent) { status = "TP"; tp++; }
                else { status = "FN"; fn++; }
            }
            else
            {
                if (detectedPresent) { status = "FP"; fp++; }
                else { status = "TN"; }
            }

            report.Results.Add(new MatchResult
            {
                Template = d.Slot.Name,
                Score = 1.0 - d.ColorDistance,
                Bbox = new[] { d.ProjectedRect.X, d.ProjectedRect.Y, d.ProjectedRect.Width, d.ProjectedRect.Height },
                ColorSim = 1.0 - d.ColorDistance,
                GoodMatches = d.Sample.ValidPixelCount,
                MatchScore = detectedPresent ? 1 : 0,
                Status = status,
                ExpectedBbox = exp?.Bbox,
                ExpectedNote = exp?.Note ?? (expected == null ? "unlabeled" : null),
                WrongColorMismatch = wrongColor,
                DetectedColor = d.DetectedColor.ToString(),
                ExpectedColor = d.Slot.ColorClass.ToString(),
                PresenceStatus = d.Presence.ToString(),
                ValidPixelRatio = d.Sample.ValidRatio,
                DarkPixelRatio = d.Sample.DarkRatio
            });
        }

        report.TP = tp;
        report.FP = fp;
        report.FN = fn;
        report.WrongColorExpectedCount = wrongColorExpected;
        report.WrongColorMismatchCount = wrongColorMismatch;

        if (expected != null)
        {
            double? precision = (tp + fp) > 0 ? (double)tp / (tp + fp) : null;
            double? recall = (tp + fn) > 0 ? (double)tp / (tp + fn) : null;
            double? f1 = (precision is > 0 && recall is > 0)
                ? 2 * precision.Value * recall.Value / (precision.Value + recall.Value)
                : (precision == 0 || recall == 0 ? 0 : null);

            report.Precision = precision;
            report.Recall = recall;
            report.F1 = f1;

            double effectiveF1 = Math.Max(0, (f1 ?? 0) - 0.1 * wrongColorMismatch);
            report.Pass = effectiveF1 >= passThreshold;
            report.Reason = $"F1={f1 ?? 0:F3}, wrongColorMismatch={wrongColorMismatch}, effectiveF1={effectiveF1:F3}, thr={passThreshold:F2}";
        }
        else
        {
            report.Pass = false;
            report.Reason = "Nessuna ground truth (expected.json) fornita per questa immagine: solo rilevazione, nessun punteggio.";
        }

        return report;
    }

    public static BatchSummaryRow ToSummaryRow(TestReport report, int templateCount)
    {
        int detected = report.Results.Count(r => r.PresenceStatus is nameof(PresenceStatus.Presente) or nameof(PresenceStatus.ColoreErrato));
        double coverage = templateCount == 0 ? 0 : (double)report.Results.Count / templateCount;
        double aggConfidence = report.Results.Count == 0 ? 0 : report.Results.Average(r => r.Score);

        return new BatchSummaryRow
        {
            TestFilename = report.TestImage,
            Pass = report.Pass,
            Precision = report.Precision,
            Recall = report.Recall,
            F1 = report.F1,
            DetectedCount = detected,
            TemplateCount = templateCount,
            Coverage = coverage,
            AggConfidence = aggConfidence,
            WrongColorMismatchCount = report.WrongColorMismatchCount,
            Reason = report.Reason
        };
    }
}

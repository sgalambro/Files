using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RelayBoxMatcher.Core.Models;

namespace RelayBoxMatcher.Core.Services;

/// <summary>Scrive gli output di un run di match: test_report.json, summary.csv (append, per il batch)
/// e un'immagine annotata con lo stato di ogni slot, stesso set di file già visto in git_result*.zip.</summary>
public static class ReportService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static readonly Color OkColor = Color.LimeGreen;
    private static readonly Color MissingColor = Color.Red;
    private static readonly Color WrongColorColor = Color.Orange;
    private static readonly Color UncertainColor = Color.Gold;

    public static void WriteTestReport(string outputFolder, TestReport report)
    {
        Directory.CreateDirectory(outputFolder);
        var path = Path.Combine(outputFolder, "test_report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOpts));
    }

    public static void AppendSummaryCsv(string outputFolder, BatchSummaryRow row)
    {
        Directory.CreateDirectory(outputFolder);
        var path = Path.Combine(outputFolder, "summary.csv");
        bool isNew = !File.Exists(path);

        var sb = new StringBuilder();
        if (isNew)
            sb.AppendLine("test_filename,pass,precision,recall,f1,detected_count,template_count,coverage,agg_confidence,wrong_color_mismatch_count,reason");

        sb.Append(Csv(row.TestFilename)).Append(',')
          .Append(row.Pass ? "true" : "false").Append(',')
          .Append(Num(row.Precision)).Append(',')
          .Append(Num(row.Recall)).Append(',')
          .Append(Num(row.F1)).Append(',')
          .Append(row.DetectedCount).Append(',')
          .Append(row.TemplateCount).Append(',')
          .Append(row.Coverage.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
          .Append(row.AggConfidence.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
          .Append(row.WrongColorMismatchCount).Append(',')
          .Append(Csv(row.Reason)).AppendLine();

        File.AppendAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: isNew));
    }

    private static string Num(double? v) => v.HasValue ? v.Value.ToString("F3", CultureInfo.InvariantCulture) : "";

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public static Bitmap DrawAnnotated(Bitmap testImage, IReadOnlyList<SlotDetection> detections)
    {
        var annotated = new Bitmap(testImage);
        using var g = Graphics.FromImage(annotated);
        using var font = new Font(FontFamily.GenericSansSerif, Math.Max(14, testImage.Width / 120f), FontStyle.Bold);

        foreach (var d in detections)
        {
            var color = d.Presence switch
            {
                PresenceStatus.Presente => OkColor,
                PresenceStatus.Assente => MissingColor,
                PresenceStatus.ColoreErrato => WrongColorColor,
                _ => UncertainColor
            };

            using var pen = new Pen(color, 4);
            g.DrawRectangle(pen, d.ProjectedRect);

            var label = $"{d.Slot.Name} [{d.Presence}]";
            var textPos = new PointF(d.ProjectedRect.X, Math.Max(0, d.ProjectedRect.Y - font.Height - 2));
            var size = g.MeasureString(label, font);
            using var bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            g.FillRectangle(bg, new RectangleF(textPos, size));
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString(label, font, textBrush, textPos);
        }

        return annotated;
    }

    public static void SaveAnnotated(string outputFolder, Bitmap annotated)
    {
        Directory.CreateDirectory(outputFolder);
        annotated.Save(Path.Combine(outputFolder, "test_annotated.png"), ImageFormat.Png);
    }
}

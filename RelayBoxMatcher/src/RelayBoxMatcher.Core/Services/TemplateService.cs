using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using RelayBoxMatcher.Core.Models;

namespace RelayBoxMatcher.Core.Services;

public class TemplateSlotInput
{
    public string Name { get; set; } = "";
    public ColorClass ColorClass { get; set; }
    public Rectangle Rect { get; set; }
}

/// <summary>
/// Crea e salva/carica il "modello" (template) costruito dalla foto campione: gli slot dei relè con
/// etichetta colorata (posizione + classe colore) e i punti di riferimento usati per la calibrazione
/// via omografia sulle foto di test. Schema JSON compatibile con templates_meta.json già presente nel repo,
/// con l'aggiunta di referencePoints/colorClass/sampleWidth/sampleHeight.
/// </summary>
public static class TemplateService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public const string MetaFileName = "templates_meta.json";

    public static string Save(string outputFolder, Bitmap sampleImage, IReadOnlyList<TemplateSlotInput> slots, IReadOnlyList<ReferencePoint> referencePoints)
    {
        if (slots.Count == 0)
            throw new ArgumentException("Nessuno slot definito: disegna almeno un rettangolo sul campione prima di salvare.");
        if (referencePoints.Count < 4)
            throw new ArgumentException("Servono almeno 4 punti di riferimento sul campione per poter calibrare le foto di test.");

        Directory.CreateDirectory(outputFolder);
        var templatesFolder = Path.Combine(outputFolder, "templates");
        Directory.CreateDirectory(templatesFolder);

        var template = new RelayTemplate
        {
            CreatedAt = DateTime.UtcNow,
            SampleWidth = sampleImage.Width,
            SampleHeight = sampleImage.Height,
            ReferencePoints = referencePoints.ToList()
        };

        int index = 0;
        foreach (var slot in slots)
        {
            var rect = Rectangle.Intersect(slot.Rect, new Rectangle(0, 0, sampleImage.Width, sampleImage.Height));
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new ArgumentException($"Lo slot '{slot.Name}' ha un'area nulla o fuori dai bordi dell'immagine.");

            var cropPath = Path.Combine(templatesFolder, $"{slot.Name}.png");
            using (var crop = sampleImage.Clone(rect, sampleImage.PixelFormat))
            {
                crop.Save(cropPath, ImageFormat.Png);
            }

            template.Templates.Add(new TemplateSlot
            {
                Index = index++,
                Name = slot.Name,
                Image = $"templates/{slot.Name}.png",
                Bbox = new BBox(rect.X, rect.Y, rect.Width, rect.Height),
                Width = rect.Width,
                Height = rect.Height,
                Md5 = ComputeMd5(cropPath),
                ColorClass = slot.ColorClass
            });
        }

        var metaPath = Path.Combine(outputFolder, MetaFileName);
        File.WriteAllText(metaPath, JsonSerializer.Serialize(template, JsonOpts));
        return metaPath;
    }

    public static RelayTemplate Load(string metaPath)
    {
        var json = File.ReadAllText(metaPath);
        return JsonSerializer.Deserialize<RelayTemplate>(json, JsonOpts)
               ?? throw new InvalidDataException("templates_meta.json non è in un formato valido.");
    }

    public static Bitmap LoadSlotCrop(string templateFolder, TemplateSlot slot) =>
        new(Path.Combine(templateFolder, slot.Image));

    private static string ComputeMd5(string path)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(path);
        var hash = md5.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

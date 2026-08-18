using System.Text.Json.Serialization;

namespace RelayBoxMatcher.Core.Models;

/// <summary>Rettangolo in pixel nello spazio dell'immagine campione. Stesso schema di templates_meta.json già presente nel repo.</summary>
public class BBox
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }

    public BBox() { }
    public BBox(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }

    public System.Drawing.Rectangle ToRectangle() => new(X, Y, W, H);
}

/// <summary>Un punto di riferimento fisico (es. vite, angolo scatola) usato per calcolare l'omografia
/// campione -> immagine di test. Ne servono almeno 4, non allineati, per ogni set di template.</summary>
public class ReferencePoint
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
}

/// <summary>Uno dei relè con etichetta colorata individuati sul campione.</summary>
public class TemplateSlot
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("image")] public string Image { get; set; } = "";
    [JsonPropertyName("bbox")] public BBox Bbox { get; set; } = new();
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("md5")] public string Md5 { get; set; } = "";
    [JsonPropertyName("colorClass")] public ColorClass ColorClass { get; set; } = ColorClass.Sconosciuto;
}

/// <summary>Radice di templates_meta.json: il "modello" costruito a partire dalla foto campione.</summary>
public class RelayTemplate
{
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("sampleWidth")] public int SampleWidth { get; set; }
    [JsonPropertyName("sampleHeight")] public int SampleHeight { get; set; }
    [JsonPropertyName("referencePoints")] public List<ReferencePoint> ReferencePoints { get; set; } = new();
    [JsonPropertyName("templates")] public List<TemplateSlot> Templates { get; set; } = new();
}

using System.Text.Json.Serialization;

namespace RelayBoxMatcher.Core.Models;

/// <summary>Ground truth opzionale per un'immagine di test, stesso schema di expected.json già presente nel repo.</summary>
public class ExpectedAnnotation
{
    [JsonPropertyName("template")] public string Template { get; set; } = "";
    [JsonPropertyName("present")] public bool Present { get; set; }
    [JsonPropertyName("bbox")] public int[]? Bbox { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public class ExpectedImage
{
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("expected")] public List<ExpectedAnnotation> Expected { get; set; } = new();
}

public class ExpectedSet
{
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("images")] public List<ExpectedImage> Images { get; set; } = new();

    public ExpectedImage? FindByFilename(string filename) =>
        Images.FirstOrDefault(i => string.Equals(i.Filename, filename, StringComparison.OrdinalIgnoreCase));
}

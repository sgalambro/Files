namespace RelayBoxMatcher.Core.Models;

/// <summary>
/// Esito del match per un singolo slot/template su un'immagine di test.
/// I nomi dei campi restano quelli già usati in test_report.json (PascalCase, System.Text.Json di default)
/// per compatibilità con report generati in precedenza; i campi sotto "Estensioni" sono nuovi e vengono
/// semplicemente ignorati da un lettore che conosce solo lo schema originale.
/// </summary>
public class MatchResult
{
    public string Template { get; set; } = "";
    public double Score { get; set; }
    public int[] Bbox { get; set; } = Array.Empty<int>();
    public double ColorSim { get; set; }
    public int GoodMatches { get; set; }
    public int MatchScore { get; set; }
    public string Status { get; set; } = "TN";
    public int[]? ExpectedBbox { get; set; }
    public string? ExpectedNote { get; set; }
    public bool WrongColorMismatch { get; set; }

    // --- Estensioni ---
    public string DetectedColor { get; set; } = "";
    public string ExpectedColor { get; set; } = "";
    public string PresenceStatus { get; set; } = "";
    public double ValidPixelRatio { get; set; }
    public double DarkPixelRatio { get; set; }
}

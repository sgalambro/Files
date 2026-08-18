namespace RelayBoxMatcher.Core.Models;

/// <summary>Report completo per un'immagine di test, stesso schema di test_report.json già presente nel repo.</summary>
public class TestReport
{
    public string TestImage { get; set; } = "";
    public List<MatchResult> Results { get; set; } = new();
    public int TP { get; set; }
    public int FP { get; set; }
    public int FN { get; set; }
    public double? Precision { get; set; }
    public double? Recall { get; set; }
    public double? F1 { get; set; }
    public bool Pass { get; set; }
    public string Reason { get; set; } = "";
    public int WrongColorExpectedCount { get; set; }
    public int WrongColorMismatchCount { get; set; }
}

/// <summary>Una riga di summary.csv (usato in modalità batch su più immagini di test).</summary>
public class BatchSummaryRow
{
    public string TestFilename { get; set; } = "";
    public bool Pass { get; set; }
    public double? Precision { get; set; }
    public double? Recall { get; set; }
    public double? F1 { get; set; }
    public int DetectedCount { get; set; }
    public int TemplateCount { get; set; }
    public double Coverage { get; set; }
    public double AggConfidence { get; set; }
    public int WrongColorMismatchCount { get; set; }
    public string Reason { get; set; } = "";
}

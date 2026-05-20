namespace Unified.Models.Reports;

public class FTDLanguageStat
{
    public int    Id        { get; set; }
    public int    ReportId  { get; set; }
    public TeamReport? Report { get; set; }
    public string Language  { get; set; } = string.Empty;
    public int    FTDCount  { get; set; }
}

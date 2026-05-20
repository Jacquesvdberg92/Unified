namespace Unified.Models.CsLiveHelp;

public class CsRequestType
{
    public int    Id     { get; set; }
    public string Name   { get; set; } = string.Empty;

    /// <summary>True for the "Other" option — requires a CustomDescription.</summary>
    public bool   IsOther { get; set; }
}

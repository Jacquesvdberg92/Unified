namespace Unified.Models.Performance;

public enum ReviewCategory
{
    Ticket,
    Chat,
    Call
}

public class ReviewItem
{
    public int    Id             { get; set; }

    public int    ReviewId       { get; set; }
    public PerformanceReview? Review { get; set; }

    public ReviewCategory Category { get; set; }
    public string  ReferenceId  { get; set; } = string.Empty;

    /// <summary>1–10 rating; validated server-side.</summary>
    public int     Rating        { get; set; }

    public string? Positive      { get; set; }
    public string? Negative      { get; set; }

    public bool    ActionRequired { get; set; }
    public string? ActionNote    { get; set; }
}

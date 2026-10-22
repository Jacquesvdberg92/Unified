using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;

namespace Unified.Models.BugReports;

public enum BugReportStatus
{
    Pending = 0, // saved locally, not yet on GitHub (GitHub unreachable or not configured)
    Sent = 1
}

/// <summary>
/// A user-submitted bug report. Each one is sent to GitHub as an issue; the local row keeps
/// the link and lets a report be retried if GitHub was unreachable when it was submitted.
/// </summary>
public class BugReport
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? StepsToReproduce { get; set; }

    [StringLength(1000)]
    public string? PageUrl { get; set; }

    /// <summary>Markdown block of system details collected when the report was submitted.</summary>
    public string SystemInfo { get; set; } = string.Empty;

    /// <summary>File name of the screenshot under App_Data/bug-reports, kept until it is uploaded.</summary>
    [StringLength(260)]
    public string? ScreenshotFileName { get; set; }

    /// <summary>Where the screenshot ended up on GitHub, so a retry does not upload it twice.</summary>
    [StringLength(512)]
    public string? ScreenshotUrl { get; set; }

    public BugReportStatus Status { get; set; } = BugReportStatus.Pending;

    public int? GitHubIssueNumber { get; set; }

    [StringLength(512)]
    public string? GitHubIssueUrl { get; set; }

    public int SendAttempts { get; set; }

    [StringLength(1000)]
    public string? LastError { get; set; }

    [Required]
    public string ReporterId { get; set; } = string.Empty;

    public AppUser? Reporter { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }
}

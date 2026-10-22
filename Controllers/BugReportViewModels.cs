using System.ComponentModel.DataAnnotations;
using Unified.Models.BugReports;

namespace Unified.Controllers;

public sealed class BugReportInputModel
{
    [Required]
    [StringLength(120, MinimumLength = 5)]
    [Display(Name = "Short summary")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    [Display(Name = "What happened? What did you expect instead?")]
    public string Description { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Steps to reproduce (optional)")]
    public string? StepsToReproduce { get; set; }

    /// <summary>The page the user was on when they clicked Report a bug.</summary>
    [StringLength(1000)]
    public string? PageUrl { get; set; }

    /// <summary>The ErrorLogs row when the report was started from the error page.</summary>
    public long? ErrorId { get; set; }

    /// <summary>JSON gathered by the browser: screen size, time zone, recent console errors, etc.</summary>
    [StringLength(20000)]
    public string? ClientInfo { get; set; }
}

public sealed class BugReportIndexViewModel
{
    public bool ShowAll { get; set; }
    public bool CanManage { get; set; }
    public bool GitHubConfigured { get; set; }
    public IReadOnlyList<BugReportRowViewModel> Rows { get; set; } = [];
}

public sealed class BugReportRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public BugReportStatus Status { get; set; }
    public int? GitHubIssueNumber { get; set; }
    public string? GitHubIssueUrl { get; set; }
    public string? LastError { get; set; }
    public int SendAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
}

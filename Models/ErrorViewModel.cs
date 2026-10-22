namespace Unified.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>HTTP status shown on the page; 500 for unhandled exceptions.</summary>
    public int StatusCode { get; set; } = 500;

    /// <summary>The ErrorLogs row for an unhandled exception, if it was saved.</summary>
    public long? ErrorId { get; set; }

    /// <summary>The local path (and query) that failed, if known.</summary>
    public string? OriginalPath { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public string? ErrorReference => ErrorId is long id ? $"ERR-{id}" : null;
}

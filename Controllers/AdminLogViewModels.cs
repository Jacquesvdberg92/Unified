namespace Unified.Controllers;

public sealed class AdminActivityLogIndexViewModel
{
    public string? UserFilter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<AdminActivityLogRowViewModel> Rows { get; set; } = [];
}

public sealed class AdminActivityLogRowViewModel
{
    public long Id { get; set; }
    public string? UserName { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class AdminErrorLogIndexViewModel
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<AdminErrorLogRowViewModel> Rows { get; set; } = [];
}

public sealed class AdminErrorLogRowViewModel
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public sealed class AdminErrorLogDetailViewModel
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
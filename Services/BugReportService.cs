using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.BugReports;

namespace Unified.Services;

/// <summary>Details about where and how a bug report was submitted, used to build its system info.</summary>
public record BugReportContext(
    string ReporterName,
    IReadOnlyList<string> ReporterRoles,
    string? PageUrl,
    string? UserAgent,
    string EnvironmentName,
    DateTime SubmittedAtUtc,
    string? ClientInfoJson,
    long? ErrorLogId = null);

/// <summary>
/// Stores bug reports and sends each one to GitHub as an issue. A report that can't be sent
/// straight away stays Pending and is retried by <see cref="BugReportRetryService"/>.
/// </summary>
public class BugReportService
{
    public static readonly HashSet<string> AllowedScreenshotExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    public const long MaxScreenshotBytes = 5 * 1024 * 1024;
    public const int MaxSendAttempts = 10;

    // Keeps a stuck submit button (or a script) from flooding GitHub with issues
    public const int MaxReportsPerWindow = 5;
    public static readonly TimeSpan ReportWindow = TimeSpan.FromMinutes(10);

    private const int MaxClientValueLength = 300;
    private const int MaxConsoleErrors = 10;
    private const int MaxServerErrors = 5;
    private const int StackTraceLines = 12;
    private const int ReportedErrorStackTraceLines = 40;

    // Client info keys shown in the system info table, in this order, with their labels.
    private static readonly (string Key, string Label)[] ClientFields =
    [
        ("screen", "Screen"),
        ("viewport", "Viewport"),
        ("pixelRatio", "Pixel ratio"),
        ("platform", "Platform"),
        ("language", "Language"),
        ("timeZone", "Time zone"),
        ("theme", "Theme"),
        ("online", "Online"),
        ("cookiesEnabled", "Cookies enabled"),
        ("referrer", "Came from"),
        ("localTime", "Local time")
    ];

    private readonly AppDbContext _db;
    private readonly GitHubIssueService _github;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BugReportService> _logger;

    public BugReportService(AppDbContext db, GitHubIssueService github, IWebHostEnvironment env, ILogger<BugReportService> logger)
    {
        _db = db;
        _github = github;
        _env = env;
        _logger = logger;
    }

    // Kept outside wwwroot so pending screenshots aren't publicly served before they are sent.
    private string ScreenshotFolder => Path.Combine(_env.ContentRootPath, "App_Data", "bug-reports");

    public async Task<bool> IsOverSubmitLimitAsync(string reporterId, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow - ReportWindow;
        var recent = await _db.BugReports.CountAsync(r => r.ReporterId == reporterId && r.CreatedAt >= since, cancellationToken);
        return recent >= MaxReportsPerWindow;
    }

    /// <summary>Saves the report and tries to send it to GitHub right away.</summary>
    public async Task<BugReport> SubmitAsync(BugReport report, BugReportContext context,
        Stream? screenshot, string? screenshotExtension, CancellationToken cancellationToken = default)
    {
        report.SystemInfo = await BuildSystemInfoAsync(report.ReporterId, context, cancellationToken);
        report.PageUrl = Truncate(context.PageUrl, 1000);
        report.CreatedAt = context.SubmittedAtUtc;
        report.Status = BugReportStatus.Pending;

        if (screenshot is not null && screenshotExtension is not null)
        {
            Directory.CreateDirectory(ScreenshotFolder);
            var fileName = $"{Guid.NewGuid()}{screenshotExtension}";
            await using var fs = File.Create(Path.Combine(ScreenshotFolder, fileName));
            await screenshot.CopyToAsync(fs, cancellationToken);
            report.ScreenshotFileName = fileName;
        }

        _db.BugReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        await TrySendAsync(report, cancellationToken);
        return report;
    }

    /// <summary>Sends a pending report to GitHub. Returns false (and records why) if it couldn't.</summary>
    public async Task<bool> TrySendAsync(BugReport report, CancellationToken cancellationToken = default)
    {
        if (report.Status == BugReportStatus.Sent)
            return true;

        report.SendAttempts++;
        try
        {
            var screenshotPath = report.ScreenshotFileName is { Length: > 0 } name ? Path.Combine(ScreenshotFolder, name) : null;
            if (report.ScreenshotUrl is null && screenshotPath is not null && File.Exists(screenshotPath))
            {
                var bytes = await File.ReadAllBytesAsync(screenshotPath, cancellationToken);
                report.ScreenshotUrl = await _github.UploadScreenshotAsync(bytes, report.ScreenshotFileName!, cancellationToken);
            }

            var issue = await _github.CreateIssueAsync($"[Bug] {report.Title}", BuildIssueBody(report), cancellationToken);

            report.Status = BugReportStatus.Sent;
            report.GitHubIssueNumber = issue.Number;
            report.GitHubIssueUrl = issue.HtmlUrl;
            report.SentAt = DateTime.UtcNow;
            report.LastError = null;

            DeleteScreenshotFile(report);
            report.ScreenshotFileName = null;
        }
        catch (GitHubIssueException ex)
        {
            report.LastError = Truncate(ex.Message, 1000);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sending bug report {Id} to GitHub failed.", report.Id);
            report.LastError = "Unexpected error while sending to GitHub.";
        }

        await _db.SaveChangesAsync(cancellationToken);
        return report.Status == BugReportStatus.Sent;
    }

    public void DeleteScreenshotFile(BugReport report)
    {
        if (report.ScreenshotFileName is not { Length: > 0 } name)
            return;

        var path = Path.Combine(ScreenshotFolder, name);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Retries every pending report that hasn't used up its attempts. Returns how many were sent.</summary>
    public async Task<int> RetryPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_github.IsConfigured)
            return 0;

        var pending = await _db.BugReports
            .Where(r => r.Status == BugReportStatus.Pending && r.SendAttempts < MaxSendAttempts)
            .OrderBy(r => r.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var report in pending)
        {
            if (await TrySendAsync(report, cancellationToken))
                sent++;
        }
        return sent;
    }

    public static string BuildIssueBody(BugReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### What happened").AppendLine().AppendLine(report.Description.Trim()).AppendLine();

        if (!string.IsNullOrWhiteSpace(report.StepsToReproduce))
            sb.AppendLine("### Steps to reproduce").AppendLine().AppendLine(report.StepsToReproduce.Trim()).AppendLine();

        if (!string.IsNullOrWhiteSpace(report.ScreenshotUrl))
            sb.AppendLine("### Screenshot").AppendLine().AppendLine($"![Screenshot]({report.ScreenshotUrl})").AppendLine();

        sb.AppendLine("### System information").AppendLine().AppendLine(report.SystemInfo.Trim()).AppendLine();
        sb.AppendLine("---").Append($"_Submitted from Unified's Report a bug form (report #{report.Id})._");
        return sb.ToString();
    }

    private async Task<string> BuildSystemInfoAsync(string reporterId, BugReportContext context, CancellationToken cancellationToken)
    {
        // Only the reporter's own errors: the error id comes from the URL and could be anyone's
        ServerError? reportedError = null;
        if (context.ErrorLogId is long errorId)
        {
            reportedError = await _db.ErrorLogs
                .AsNoTracking()
                .Where(e => e.Id == errorId && e.UserId == reporterId)
                .Select(e => new ServerError(e.Id, e.Timestamp, e.Method, e.Path, e.ExceptionType, e.Message, e.StackTrace))
                .FirstOrDefaultAsync(cancellationToken);
        }

        var since = context.SubmittedAtUtc.AddHours(-24);
        var reportedErrorId = reportedError?.Id ?? 0;
        var serverErrors = await _db.ErrorLogs
            .AsNoTracking()
            .Where(e => e.UserId == reporterId && e.Timestamp >= since && e.Id != reportedErrorId)
            .OrderByDescending(e => e.Timestamp)
            .Take(MaxServerErrors)
            .Select(e => new ServerError(e.Id, e.Timestamp, e.Method, e.Path, e.ExceptionType, e.Message, e.StackTrace))
            .ToListAsync(cancellationToken);

        return FormatSystemInfo(context, serverErrors, reportedError);
    }

    public record ServerError(long Id, DateTime Timestamp, string Method, string Path, string ExceptionType, string Message, string StackTrace);

    public static string FormatSystemInfo(BugReportContext context, IReadOnlyList<ServerError> serverErrors, ServerError? reportedError = null)
    {
        var client = ParseClientInfo(context.ClientInfoJson, out var consoleErrors);

        var rows = new List<(string Label, string? Value)>
        {
            ("Reported by", context.ReporterName),
            ("Error reference", reportedError is null ? null : $"ERR-{reportedError.Id}"),
            ("Roles", context.ReporterRoles.Count > 0 ? string.Join(", ", context.ReporterRoles) : "none"),
            ("Page", context.PageUrl),
            ("Submitted (UTC)", context.SubmittedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")),
            ("App version", AppVersion),
            ("Environment", context.EnvironmentName),
            ("Server", $"{RuntimeInformation.FrameworkDescription} on {RuntimeInformation.OSDescription}"),
            ("Browser", context.UserAgent)
        };
        rows.AddRange(ClientFields.Select(f => (f.Label, client.GetValueOrDefault(f.Key))));

        var sb = new StringBuilder();
        sb.AppendLine("| | |").AppendLine("|---|---|");
        foreach (var (label, value) in rows.Where(r => !string.IsNullOrWhiteSpace(r.Value)))
            sb.AppendLine($"| {label} | {TableCell(value!)} |");

        if (reportedError is not null)
        {
            sb.AppendLine().AppendLine($"**The error this report is about (ERR-{reportedError.Id})**").AppendLine();
            sb.AppendLine(CodeBlock(FormatServerError(reportedError, ReportedErrorStackTraceLines)));
        }

        if (consoleErrors.Count > 0)
        {
            sb.AppendLine().AppendLine("**Recent browser errors**").AppendLine();
            sb.AppendLine(CodeBlock(string.Join("\n", consoleErrors)));
        }

        if (serverErrors.Count > 0)
        {
            sb.AppendLine().AppendLine("**Recent server errors for this user (last 24h)**").AppendLine();
            sb.AppendLine(CodeBlock(string.Join("\n\n", serverErrors.Select(e => FormatServerError(e, StackTraceLines)))));
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatServerError(ServerError e, int stackTraceLines) =>
        $"{e.Timestamp:yyyy-MM-dd HH:mm:ss} {e.Method} {e.Path}\n{e.ExceptionType}: {e.Message}\n" +
        string.Join("\n", e.StackTrace.Split('\n').Take(stackTraceLines).Select(l => l.TrimEnd()));

    private static Dictionary<string, string> ParseClientInfo(string? json, out List<string> consoleErrors)
    {
        var values = new Dictionary<string, string>();
        consoleErrors = [];
        if (string.IsNullOrWhiteSpace(json))
            return values;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return values;

            foreach (var (key, _) in ClientFields)
            {
                if (doc.RootElement.TryGetProperty(key, out var el) && el.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null))
                    values[key] = Truncate(el.ToString(), MaxClientValueLength)!;
            }

            if (doc.RootElement.TryGetProperty("consoleErrors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                consoleErrors = errors.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => Truncate(e.GetString(), 500)!)
                    .TakeLast(MaxConsoleErrors)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // Client info is best effort; a malformed payload just means fewer details.
        }

        return values;
    }

    private static string AppVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "unknown";

    private static string TableCell(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|");

    private static string CodeBlock(string text) => $"```text\n{text.Replace("```", "'''")}\n```";

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}

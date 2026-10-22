using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Models.BugReports;
using Unified.Models.Identity;
using Unified.Models.Logging;
using Unified.Services;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class BugReportServiceTests
{
    /// <summary>Answers GitHub API calls by "METHOD path" and records every request it sees.</summary>
    private sealed class GitHubStub : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _routes = new();
        public List<(string Route, string? Body, HttpRequestMessage Request)> Calls { get; } = [];
        public bool Offline { get; set; }

        public GitHubStub On(string route, HttpStatusCode status, object body)
        {
            _routes[route] = (status, JsonSerializer.Serialize(body));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Offline)
                throw new HttpRequestException("offline");

            var route = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            var body  = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((route, body, request));

            // Contents uploads land on a dated path, so match them by prefix.
            var match = _routes.FirstOrDefault(r => route == r.Key || (r.Key.EndsWith('*') && route.StartsWith(r.Key[..^1])));
            var (status, json) = match.Key is null ? (HttpStatusCode.NotFound, "{}") : match.Value;
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubEnv(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Unified";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
    }

    private const string Repo = "/repos/acme/unified";

    private static GitHubIssueService BuildGitHub(GitHubStub stub, string? token = "test-token")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GitHub:Token"]         = token,
            ["GitHub:Owner"]         = "acme",
            ["GitHub:Repo"]          = "unified",
            ["GitHub:IssueLabels:0"] = "bug",
            ["GitHub:IssueLabels:1"] = "user-report"
        }).Build();
        return new GitHubIssueService(new StubFactory(stub), config, NullLogger<GitHubIssueService>.Instance);
    }

    private static async Task<(Unified.Data.AppDbContext db, BugReportService svc, string root)> BuildAsync(string name, GitHubStub stub, string? token = "test-token")
    {
        var db = DbHelper.CreateInMemory(name);
        db.Users.Add(new AppUser { Id = "u1", UserName = "u1", DisplayName = "User One" });
        await db.SaveChangesAsync();

        var root = Path.Combine(Path.GetTempPath(), "unified-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var svc = new BugReportService(db, BuildGitHub(stub, token), new StubEnv(root), NullLogger<BugReportService>.Instance);
        return (db, svc, root);
    }

    private static BugReportContext Context(string? clientInfo = null) => new(
        ReporterName: "User One",
        ReporterRoles: ["CSAgent"],
        PageUrl: "https://unified.local/Withdrawals?page=2",
        UserAgent: "Mozilla/5.0 Test",
        EnvironmentName: "Test",
        SubmittedAtUtc: new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc),
        ClientInfoJson: clientInfo);

    private static BugReport NewReport() => new()
    {
        Title = "Save button does nothing",
        Description = "Clicking save on a withdrawal shows no feedback.",
        StepsToReproduce = "1. Open a withdrawal\n2. Click save",
        ReporterId = "u1"
    };

    private static GitHubStub WorkingGitHub() => new GitHubStub()
        .On($"POST {Repo}/issues", HttpStatusCode.Created, new { number = 42, html_url = "https://github.com/acme/unified/issues/42" })
        .On($"GET {Repo}/git/ref/heads/bug-report-screenshots", HttpStatusCode.OK, new { @object = new { sha = "abc" } })
        .On($"PUT {Repo}/contents/bug-reports/*", HttpStatusCode.Created,
            new { content = new { download_url = "https://raw.githubusercontent.com/acme/unified/bug-report-screenshots/shot.png" } });

    // ── Submit ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_CreatesLabelledIssueWithDetails_AndMarksSent()
    {
        var stub = WorkingGitHub();
        var (db, svc, _) = await BuildAsync(nameof(Submit_CreatesLabelledIssueWithDetails_AndMarksSent), stub);

        var report = await svc.SubmitAsync(NewReport(), Context(), null, null);

        Assert.Equal(BugReportStatus.Sent, report.Status);
        Assert.Equal(42, report.GitHubIssueNumber);
        Assert.Equal("https://github.com/acme/unified/issues/42", report.GitHubIssueUrl);

        var call = Assert.Single(stub.Calls);
        Assert.Equal("Bearer", call.Request.Headers.Authorization?.Scheme);
        using var json = JsonDocument.Parse(call.Body!);
        Assert.Equal("[Bug] Save button does nothing", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(["bug", "user-report"], json.RootElement.GetProperty("labels").EnumerateArray().Select(l => l.GetString()));
        var body = json.RootElement.GetProperty("body").GetString()!;
        Assert.Contains("Clicking save on a withdrawal", body);
        Assert.Contains("### Steps to reproduce", body);
        Assert.Contains("| Reported by | User One |", body);
        Assert.Contains("| Page | https://unified.local/Withdrawals?page=2 |", body);
        Assert.DoesNotContain("### Screenshot", body);

        Assert.Equal(BugReportStatus.Sent, (await db.BugReports.SingleAsync()).Status);
    }

    [Fact]
    public async Task Submit_WithScreenshot_UploadsItEmbedsItAndDeletesLocalCopy()
    {
        var stub = WorkingGitHub();
        var (_, svc, root) = await BuildAsync(nameof(Submit_WithScreenshot_UploadsItEmbedsItAndDeletesLocalCopy), stub);

        using var image = new MemoryStream([1, 2, 3]);
        var report = await svc.SubmitAsync(NewReport(), Context(), image, ".png");

        Assert.Equal(BugReportStatus.Sent, report.Status);
        var upload = stub.Calls.Single(c => c.Route.StartsWith($"PUT {Repo}/contents/bug-reports/"));
        using (var json = JsonDocument.Parse(upload.Body!))
        {
            Assert.Equal(Convert.ToBase64String([1, 2, 3]), json.RootElement.GetProperty("content").GetString());
            Assert.Equal("bug-report-screenshots", json.RootElement.GetProperty("branch").GetString());
        }

        var issue = stub.Calls.Single(c => c.Route == $"POST {Repo}/issues");
        Assert.Contains("![Screenshot](https://raw.githubusercontent.com/acme/unified/bug-report-screenshots/shot.png)", issue.Body);
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "App_Data", "bug-reports")));
    }

    [Fact]
    public async Task Submit_CreatesScreenshotBranchFromDefaultBranchWhenMissing()
    {
        var stub = WorkingGitHub()
            .On($"GET {Repo}/git/ref/heads/bug-report-screenshots", HttpStatusCode.NotFound, new { message = "Not Found" })
            .On($"GET {Repo}", HttpStatusCode.OK, new { default_branch = "main" })
            .On($"GET {Repo}/git/ref/heads/main", HttpStatusCode.OK, new { @object = new { sha = "main-sha" } })
            .On($"POST {Repo}/git/refs", HttpStatusCode.Created, new { @ref = "refs/heads/bug-report-screenshots" });
        var (_, svc, _) = await BuildAsync(nameof(Submit_CreatesScreenshotBranchFromDefaultBranchWhenMissing), stub);

        using var image = new MemoryStream([1]);
        var report = await svc.SubmitAsync(NewReport(), Context(), image, ".png");

        Assert.Equal(BugReportStatus.Sent, report.Status);
        var create = stub.Calls.Single(c => c.Route == $"POST {Repo}/git/refs");
        using var json = JsonDocument.Parse(create.Body!);
        Assert.Equal("refs/heads/bug-report-screenshots", json.RootElement.GetProperty("ref").GetString());
        Assert.Equal("main-sha", json.RootElement.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task Submit_WhenGitHubUnreachable_KeepsReportPendingWithScreenshot()
    {
        var stub = WorkingGitHub();
        stub.Offline = true;
        var (_, svc, root) = await BuildAsync(nameof(Submit_WhenGitHubUnreachable_KeepsReportPendingWithScreenshot), stub);

        using var image = new MemoryStream([1, 2, 3]);
        var report = await svc.SubmitAsync(NewReport(), Context(), image, ".png");

        Assert.Equal(BugReportStatus.Pending, report.Status);
        Assert.Equal(1, report.SendAttempts);
        Assert.Equal("GitHub could not be reached.", report.LastError);
        Assert.Single(Directory.GetFiles(Path.Combine(root, "App_Data", "bug-reports")));
    }

    [Fact]
    public async Task Submit_WhenNotConfigured_KeepsReportPending()
    {
        var stub = WorkingGitHub();
        var (_, svc, _) = await BuildAsync(nameof(Submit_WhenNotConfigured_KeepsReportPending), stub, token: null);

        var report = await svc.SubmitAsync(NewReport(), Context(), null, null);

        Assert.Equal(BugReportStatus.Pending, report.Status);
        Assert.Contains("not configured", report.LastError);
        Assert.Empty(stub.Calls);
    }

    [Fact]
    public async Task Submit_WhenTokenRejected_RecordsFriendlyError()
    {
        var stub = new GitHubStub().On($"POST {Repo}/issues", HttpStatusCode.Unauthorized, new { message = "Bad credentials" });
        var (_, svc, _) = await BuildAsync(nameof(Submit_WhenTokenRejected_RecordsFriendlyError), stub);

        var report = await svc.SubmitAsync(NewReport(), Context(), null, null);

        Assert.Equal(BugReportStatus.Pending, report.Status);
        Assert.Equal("GitHub rejected the token. Check GitHub:Token.", report.LastError);
    }

    // ── Retry ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryPending_SendsReportsOnceGitHubIsBack_AndDoesNotReuploadScreenshot()
    {
        // The screenshot made it up but the issue failed; the retry should only create the issue.
        var stub = WorkingGitHub().On($"POST {Repo}/issues", HttpStatusCode.BadGateway, new { });
        var (db, svc, _) = await BuildAsync(nameof(RetryPending_SendsReportsOnceGitHubIsBack_AndDoesNotReuploadScreenshot), stub);
        using var image = new MemoryStream([1]);
        var report = await svc.SubmitAsync(NewReport(), Context(), image, ".png");
        Assert.Equal(BugReportStatus.Pending, report.Status);
        Assert.NotNull(report.ScreenshotUrl);

        stub.On($"POST {Repo}/issues", HttpStatusCode.Created, new { number = 7, html_url = "https://github.com/acme/unified/issues/7" });
        var sent = await svc.RetryPendingAsync();

        Assert.Equal(1, sent);
        Assert.Equal(7, (await db.BugReports.SingleAsync()).GitHubIssueNumber);
        Assert.Single(stub.Calls, c => c.Route.StartsWith($"PUT {Repo}/contents/"));
    }

    [Fact]
    public async Task RetryPending_SkipsReportsThatUsedUpTheirAttempts()
    {
        var stub = WorkingGitHub();
        var (db, svc, _) = await BuildAsync(nameof(RetryPending_SkipsReportsThatUsedUpTheirAttempts), stub);
        var report = NewReport();
        report.SendAttempts = BugReportService.MaxSendAttempts;
        db.BugReports.Add(report);
        await db.SaveChangesAsync();

        Assert.Equal(0, await svc.RetryPendingAsync());
        Assert.Empty(stub.Calls);
    }

    // ── Submit limit ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsOverSubmitLimit_TrueAfterFiveRecentReports()
    {
        var (db, svc, _) = await BuildAsync(nameof(IsOverSubmitLimit_TrueAfterFiveRecentReports), WorkingGitHub());
        for (var i = 0; i < BugReportService.MaxReportsPerWindow - 1; i++)
            db.BugReports.Add(new BugReport { Title = "t", Description = "d", ReporterId = "u1", CreatedAt = DateTime.UtcNow });
        db.BugReports.Add(new BugReport { Title = "old", Description = "d", ReporterId = "u1", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync();

        Assert.False(await svc.IsOverSubmitLimitAsync("u1"));

        db.BugReports.Add(new BugReport { Title = "t", Description = "d", ReporterId = "u1", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.True(await svc.IsOverSubmitLimitAsync("u1"));
    }

    // ── System info ───────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_IncludesRecentServerErrorsForThisUserOnly()
    {
        var stub = WorkingGitHub();
        var (db, svc, _) = await BuildAsync(nameof(Submit_IncludesRecentServerErrorsForThisUserOnly), stub);
        var at = Context().SubmittedAtUtc;
        db.ErrorLogs.AddRange(
            new ErrorLog { UserId = "u1", Path = "/Withdrawals/Save", Method = "POST", ExceptionType = "System.InvalidOperationException", Message = "Boom", StackTrace = "at A\nat B", Timestamp = at.AddMinutes(-5) },
            new ErrorLog { UserId = "u2", Path = "/Other", Method = "GET", ExceptionType = "X", Message = "Someone else", StackTrace = "", Timestamp = at.AddMinutes(-5) },
            new ErrorLog { UserId = "u1", Path = "/Old", Method = "GET", ExceptionType = "X", Message = "Too old", StackTrace = "", Timestamp = at.AddDays(-2) });
        await db.SaveChangesAsync();

        var report = await svc.SubmitAsync(NewReport(), Context(), null, null);

        Assert.Contains("POST /Withdrawals/Save", report.SystemInfo);
        Assert.Contains("System.InvalidOperationException: Boom", report.SystemInfo);
        Assert.DoesNotContain("Someone else", report.SystemInfo);
        Assert.DoesNotContain("Too old", report.SystemInfo);
    }

    [Fact]
    public void FormatSystemInfo_ListsClientDetailsAndConsoleErrors()
    {
        var clientInfo = JsonSerializer.Serialize(new
        {
            screen = "1920x1080",
            timeZone = "Europe/Malta",
            theme = "dark",
            unknownKey = "ignored",
            consoleErrors = new[] { "TypeError: x is undefined" }
        });

        var info = BugReportService.FormatSystemInfo(Context(clientInfo), []);

        Assert.Contains("| Screen | 1920x1080 |", info);
        Assert.Contains("| Time zone | Europe/Malta |", info);
        Assert.Contains("| Theme | dark |", info);
        Assert.Contains("| Roles | CSAgent |", info);
        Assert.Contains("| Browser | Mozilla/5.0 Test |", info);
        Assert.Contains("TypeError: x is undefined", info);
        Assert.DoesNotContain("ignored", info);
    }

    [Fact]
    public void FormatSystemInfo_EscapesTableCellsAndIgnoresMalformedClientInfo()
    {
        var context = Context("{not json") with { ReporterName = "Pipe | Name\nNewline" };

        var info = BugReportService.FormatSystemInfo(context, []);

        Assert.Contains("| Reported by | Pipe \\| Name Newline |", info);
        Assert.DoesNotContain("Screen", info);
    }
}

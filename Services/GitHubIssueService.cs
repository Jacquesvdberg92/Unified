using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Unified.Services;

/// <summary>Raised for GitHub failures whose message is safe to show to the user.</summary>
public class GitHubIssueException(string message) : Exception(message);

public record GitHubIssue(int Number, string HtmlUrl);

/// <summary>
/// Creates issues (and uploads screenshots for them) through the GitHub REST API.
/// Configured under the "GitHub" section of appsettings. The token needs read/write access
/// to Issues and Contents on the target repository.
/// </summary>
public class GitHubIssueService
{
    public const string HttpClientName = "GitHub";

    private const string ApiBase = "https://api.github.com";
    private const string DefaultScreenshotBranch = "bug-report-screenshots";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubIssueService> _logger;

    public GitHubIssueService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GitHubIssueService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private string? Token => _configuration["GitHub:Token"];
    private string Owner  => _configuration["GitHub:Owner"] ?? string.Empty;
    private string Repo   => _configuration["GitHub:Repo"] ?? string.Empty;
    private string ScreenshotBranch => _configuration["GitHub:ScreenshotBranch"] is { Length: > 0 } b ? b : DefaultScreenshotBranch;
    private string RepoPath => $"{ApiBase}/repos/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Repo)}";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Owner) && !string.IsNullOrWhiteSpace(Repo);

    public async Task<GitHubIssue> CreateIssueAsync(string title, string body, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var labels = _configuration.GetSection("GitHub:IssueLabels").Get<string[]>() ?? [];

        using var response = await SendAsync(HttpMethod.Post, $"{RepoPath}/issues", new { title, body, labels }, cancellationToken);
        var json = await ReadSuccessAsync(response, "create the issue", cancellationToken);

        return new GitHubIssue(json.GetProperty("number").GetInt32(), json.GetProperty("html_url").GetString() ?? string.Empty);
    }

    /// <summary>
    /// Commits the image to the screenshot branch (creating the branch from the default branch
    /// the first time) and returns a URL that renders it inline in an issue.
    /// </summary>
    public async Task<string> UploadScreenshotAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureScreenshotBranchAsync(cancellationToken);

        var path = $"bug-reports/{DateTime.UtcNow:yyyy-MM}/{fileName}";
        var payload = new
        {
            message = $"Add bug report screenshot {fileName}",
            content = Convert.ToBase64String(content),
            branch  = ScreenshotBranch
        };

        using var response = await SendAsync(HttpMethod.Put, $"{RepoPath}/contents/{path}", payload, cancellationToken);
        var json = await ReadSuccessAsync(response, "upload the screenshot", cancellationToken);

        return json.GetProperty("content").GetProperty("download_url").GetString() ?? string.Empty;
    }

    private async Task EnsureScreenshotBranchAsync(CancellationToken cancellationToken)
    {
        using (var existing = await SendAsync(HttpMethod.Get, $"{RepoPath}/git/ref/heads/{ScreenshotBranch}", null, cancellationToken))
        {
            if (existing.IsSuccessStatusCode)
                return;
            if (existing.StatusCode != HttpStatusCode.NotFound)
                await ReadSuccessAsync(existing, "look up the screenshot branch", cancellationToken);
        }

        using var repoResponse = await SendAsync(HttpMethod.Get, RepoPath, null, cancellationToken);
        var defaultBranch = (await ReadSuccessAsync(repoResponse, "read the repository", cancellationToken))
            .GetProperty("default_branch").GetString();

        using var headResponse = await SendAsync(HttpMethod.Get, $"{RepoPath}/git/ref/heads/{defaultBranch}", null, cancellationToken);
        var sha = (await ReadSuccessAsync(headResponse, "read the default branch", cancellationToken))
            .GetProperty("object").GetProperty("sha").GetString();

        using var create = await SendAsync(HttpMethod.Post, $"{RepoPath}/git/refs",
            new { @ref = $"refs/heads/{ScreenshotBranch}", sha }, cancellationToken);
        await ReadSuccessAsync(create, "create the screenshot branch", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Unified", "1.0"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (payload is not null)
            request.Content = JsonContent.Create(payload);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "GitHub request {Method} {Url} failed.", method, url);
            throw new GitHubIssueException("GitHub could not be reached.");
        }
    }

    private async Task<JsonElement> ReadSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub failed to {Action}: {Status} {Body}", action, (int)response.StatusCode, body);
            throw new GitHubIssueException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "GitHub rejected the token. Check GitHub:Token.",
                HttpStatusCode.Forbidden    => $"GitHub refused to {action}. The token may lack permission, or the rate limit was hit.",
                HttpStatusCode.NotFound     => $"GitHub could not {action}: repository not found. Check GitHub:Owner and GitHub:Repo.",
                _                           => $"GitHub could not {action} ({(int)response.StatusCode})."
            });
        }

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new GitHubIssueException("GitHub is not configured. Set GitHub:Token, GitHub:Owner and GitHub:Repo.");
    }
}

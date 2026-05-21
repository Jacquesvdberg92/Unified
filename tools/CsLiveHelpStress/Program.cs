using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

internal static class StressProgram
{
    public static async Task Main(string[] args)
    {
        var options = StressOptions.Parse(args);
        using var initialClient = new CsLiveHelpStressClient(options.BaseUrl, options.CookieHeader, options.TimeoutSeconds);

        var snapshot = await initialClient.GetRequestsSnapshotAsync(CancellationToken.None);
        Console.WriteLine($"Connected. brand={snapshot.BrandId}, type={snapshot.RequestTypeId}, own={snapshot.OwnRequestIds.Count}, open={snapshot.OpenRequestIds.Count}, renderMs={snapshot.RenderLatency.TotalMilliseconds:F1}");

        if (options.ValidateOnly)
        {
            Console.WriteLine("Validation mode complete.");
            return;
        }

        var ids = new ConcurrentDictionary<int, byte>();
        foreach (var id in snapshot.OwnRequestIds) ids.TryAdd(id, 0);

        var metrics = new MetricsCollector();
        var stopAt = DateTime.UtcNow.AddMinutes(options.DurationMinutes);
        var workerTasks = new List<Task>();

        Console.WriteLine($"Starting profile={options.Profile} workers={options.Workers} durationMin={options.DurationMinutes}...");

        for (var workerIndex = 0; workerIndex < options.Workers; workerIndex++)
        {
            var localWorker = workerIndex + 1;
            workerTasks.Add(Task.Run(async () =>
            {
                using var client = new CsLiveHelpStressClient(options.BaseUrl, options.CookieHeader, options.TimeoutSeconds);
                var localSnapshot = await client.GetRequestsSnapshotAsync(CancellationToken.None);
                var opCount = 0;

                while (DateTime.UtcNow < stopAt)
                {
                    opCount++;
                    if (opCount % options.RefreshEveryOperations == 0)
                    {
                        try
                        {
                            localSnapshot = await client.GetRequestsSnapshotAsync(CancellationToken.None);
                            foreach (var id in localSnapshot.OwnRequestIds) ids.TryAdd(id, 0);
                            metrics.Record("GET /CsLiveHelp/Requests", true, localSnapshot.RenderLatency);
                        }
                        catch
                        {
                            metrics.Record("GET /CsLiveHelp/Requests", false, TimeSpan.Zero);
                        }
                    }

                    var op = options.NextOperation();
                    var nowTicks = DateTime.UtcNow.Ticks;

                    try
                    {
                        switch (op)
                        {
                            case WorkloadOperation.CreateRequest:
                            {
                                var create = await client.CreateRequestAsync(localSnapshot, $"stress-{localWorker}-{nowTicks}", null, CancellationToken.None);
                                metrics.Record("POST /CsLiveHelp/CreateRequest", create.Success, create.Elapsed);
                                break;
                            }
                            case WorkloadOperation.EditRequest:
                            {
                                var editId = StressHelpers.PickRandomId(localSnapshot.OpenRequestIds, ids.Keys);
                                if (editId is null)
                                {
                                    metrics.Record("POST /CsLiveHelp/EditRequest (skipped)", false, TimeSpan.Zero);
                                    break;
                                }

                                var edit = await client.EditRequestAsync(localSnapshot, editId.Value, $"stress-edit-{localWorker}", null, CancellationToken.None);
                                metrics.Record("POST /CsLiveHelp/EditRequest", edit.Success, edit.Elapsed);
                                break;
                            }
                            case WorkloadOperation.AddComment:
                            {
                                var commentId = StressHelpers.PickRandomId(localSnapshot.OwnRequestIds, ids.Keys);
                                if (commentId is null)
                                {
                                    metrics.Record("POST /CsLiveHelp/AddComment (skipped)", false, TimeSpan.Zero);
                                    break;
                                }

                                var comment = await client.AddCommentAsync(localSnapshot, commentId.Value, $"stress note {nowTicks}", CancellationToken.None);
                                metrics.Record("POST /CsLiveHelp/AddComment", comment.Success, comment.Elapsed);
                                break;
                            }
                            case WorkloadOperation.RenderRequests:
                            {
                                var renderSnapshot = await client.GetRequestsSnapshotAsync(CancellationToken.None);
                                localSnapshot = renderSnapshot;
                                foreach (var id in localSnapshot.OwnRequestIds) ids.TryAdd(id, 0);
                                metrics.Record("GET /CsLiveHelp/Requests", true, renderSnapshot.RenderLatency);
                                break;
                            }
                        }
                    }
                    catch
                    {
                        metrics.Record($"{op}", false, TimeSpan.Zero);
                    }
                }
            }));
        }

        await Task.WhenAll(workerTasks);

        metrics.PrintSummary(options.DurationMinutes);
    }
}

internal sealed record StressOptions(
    Uri BaseUrl,
    string CookieHeader,
    int TimeoutSeconds,
    bool ValidateOnly,
    int Workers,
    int DurationMinutes,
    int RefreshEveryOperations,
    string Profile,
    int CreateWeight,
    int EditWeight,
    int CommentWeight,
    int RenderWeight)
{
    public static StressOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            map[key] = value;
        }

        if (!map.TryGetValue("base-url", out var baseUrlRaw) || !Uri.TryCreate(baseUrlRaw, UriKind.Absolute, out var baseUrl))
            throw new ArgumentException("Missing or invalid --base-url (example: https://localhost:5001)");

        if (!map.TryGetValue("cookie", out var cookieHeader) || string.IsNullOrWhiteSpace(cookieHeader))
            throw new ArgumentException("Missing --cookie. Provide browser Cookie header from an authenticated Account Manager session.");

        var timeoutSeconds = 60;
        if (map.TryGetValue("timeout-seconds", out var timeoutRaw) && int.TryParse(timeoutRaw, out var parsedTimeout) && parsedTimeout > 0)
            timeoutSeconds = parsedTimeout;

        var validateOnly = map.TryGetValue("validate-only", out var validateRaw)
            && bool.TryParse(validateRaw, out var parsedValidate)
            && parsedValidate;

        var profile = map.TryGetValue("profile", out var profileRaw) ? profileRaw.ToLowerInvariant() : "realistic";

        var (workers, duration, refreshEveryOps, createWeight, editWeight, commentWeight, renderWeight) = profile switch
        {
            "stress" => (8, 15, 8, 45, 25, 20, 10),
            "breakpoint" => (16, 20, 5, 50, 20, 20, 10),
            _ => (4, 10, 10, 35, 25, 25, 15)
        };

        if (map.TryGetValue("workers", out var workersRaw) && int.TryParse(workersRaw, out var parsedWorkers) && parsedWorkers > 0)
            workers = parsedWorkers;
        if (map.TryGetValue("duration-min", out var durationRaw) && int.TryParse(durationRaw, out var parsedDuration) && parsedDuration > 0)
            duration = parsedDuration;
        if (map.TryGetValue("refresh-every", out var refreshRaw) && int.TryParse(refreshRaw, out var parsedRefresh) && parsedRefresh > 0)
            refreshEveryOps = parsedRefresh;

        return new StressOptions(baseUrl, cookieHeader, timeoutSeconds, validateOnly, workers, duration, refreshEveryOps, profile, createWeight, editWeight, commentWeight, renderWeight);
    }

    public WorkloadOperation NextOperation()
    {
        var total = CreateWeight + EditWeight + CommentWeight + RenderWeight;
        var pick = Random.Shared.Next(1, total + 1);
        if (pick <= CreateWeight) return WorkloadOperation.CreateRequest;
        if (pick <= CreateWeight + EditWeight) return WorkloadOperation.EditRequest;
        if (pick <= CreateWeight + EditWeight + CommentWeight) return WorkloadOperation.AddComment;
        return WorkloadOperation.RenderRequests;
    }
}

internal enum WorkloadOperation
{
    CreateRequest,
    EditRequest,
    AddComment,
    RenderRequests
}

internal sealed class CsLiveHelpStressClient : IDisposable
{
    private static readonly Regex AntiForgeryTokenRegex = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EditModalIdRegex = new("id=\"editModal-(?<id>\\d+)\"", RegexOptions.Compiled);
    private static readonly Regex CommentModalIdRegex = new("id=\"commentModal-(?<id>\\d+)\"", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly CookieContainer _cookies;

    public CsLiveHelpStressClient(Uri baseUrl, string cookieHeader, int timeoutSeconds)
    {
        _cookies = new CookieContainer();
        SeedCookies(baseUrl, cookieHeader);

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        _http = new HttpClient(handler)
        {
            BaseAddress = baseUrl,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("CsLiveHelpStress/1.0");
    }

    public async Task<RequestsSnapshot> GetRequestsSnapshotAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var response = await _http.GetAsync("/CsLiveHelp/Requests", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        sw.Stop();

        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is { } location)
            throw new InvalidOperationException($"Redirected to {location}. The cookie is likely invalid or not an Account Manager session.");

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"GET /CsLiveHelp/Requests failed with {(int)response.StatusCode}");

        var token = MatchRequired(AntiForgeryTokenRegex, body, 1, "anti-forgery token");
        var brandId = int.Parse(MatchRequired(CreateOptionRegex("brandId"), body, "id", "brand option"), CultureInfo.InvariantCulture);
        var typeId = int.Parse(MatchRequired(CreateOptionRegex("requestTypeId"), body, "id", "request type option"), CultureInfo.InvariantCulture);

        var openIds = ParseIds(EditModalIdRegex, body);
        var ownIds = ParseIds(CommentModalIdRegex, body);

        return new RequestsSnapshot(token, brandId, typeId, ownIds, openIds, sw.Elapsed);
    }

    public Task<RequestCallResult> CreateRequestAsync(RequestsSnapshot snapshot, string clientId, string? customDescription, CancellationToken ct)
        => PostFormAsync("/CsLiveHelp/CreateRequest", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = snapshot.AntiForgeryToken,
            ["brandId"] = snapshot.BrandId.ToString(CultureInfo.InvariantCulture),
            ["requestTypeId"] = snapshot.RequestTypeId.ToString(CultureInfo.InvariantCulture),
            ["customDescription"] = customDescription ?? string.Empty,
            ["clientId"] = clientId
        }, ct);

    public Task<RequestCallResult> EditRequestAsync(RequestsSnapshot snapshot, int requestId, string clientId, string? customDescription, CancellationToken ct)
        => PostFormAsync("/CsLiveHelp/EditRequest", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = snapshot.AntiForgeryToken,
            ["id"] = requestId.ToString(CultureInfo.InvariantCulture),
            ["brandId"] = snapshot.BrandId.ToString(CultureInfo.InvariantCulture),
            ["requestTypeId"] = snapshot.RequestTypeId.ToString(CultureInfo.InvariantCulture),
            ["customDescription"] = customDescription ?? string.Empty,
            ["clientId"] = clientId
        }, ct);

    public Task<RequestCallResult> AddCommentAsync(RequestsSnapshot snapshot, int requestId, string body, CancellationToken ct)
        => PostFormAsync("/CsLiveHelp/AddComment", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = snapshot.AntiForgeryToken,
            ["id"] = requestId.ToString(CultureInfo.InvariantCulture),
            ["body"] = body
        }, ct);

    private async Task<RequestCallResult> PostFormAsync(string path, Dictionary<string, string> formValues, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var response = await _http.PostAsync(path, new FormUrlEncodedContent(formValues), ct);
        sw.Stop();

        var success = response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.SeeOther;
        var location = response.Headers.Location?.ToString();
        return new RequestCallResult(path, response.StatusCode, success, sw.Elapsed, location);
    }

    private void SeedCookies(Uri baseUrl, string cookieHeader)
    {
        var items = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var item in items)
        {
            var idx = item.IndexOf('=');
            if (idx <= 0 || idx >= item.Length - 1) continue;
            var name = item[..idx].Trim();
            var value = item[(idx + 1)..].Trim();
            if (name.Length == 0) continue;
            _cookies.Add(baseUrl, new Cookie(name, value));
        }
    }

    private static Regex CreateOptionRegex(string selectName)
        => new Regex($"<select[^>]*name=\\\"{Regex.Escape(selectName)}\\\"[\\s\\S]*?<option value=\\\"(?<id>\\d+)\\\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static List<int> ParseIds(Regex regex, string body)
    {
        var set = new HashSet<int>();
        foreach (Match match in regex.Matches(body))
        {
            var raw = match.Groups["id"].Value;
            if (int.TryParse(raw, out var id)) set.Add(id);
        }
        return set.OrderBy(x => x).ToList();
    }

    private static string MatchRequired(Regex regex, string input, int groupIndex, string label)
    {
        var match = regex.Match(input);
        if (!match.Success) throw new InvalidOperationException($"Could not parse {label} from Requests page.");
        return match.Groups[groupIndex].Value;
    }

    private static string MatchRequired(Regex regex, string input, string groupName, string label)
    {
        var match = regex.Match(input);
        if (!match.Success) throw new InvalidOperationException($"Could not parse {label} from Requests page.");
        return match.Groups[groupName].Value;
    }

    public void Dispose() => _http.Dispose();
}

internal sealed record RequestsSnapshot(
    string AntiForgeryToken,
    int BrandId,
    int RequestTypeId,
    IReadOnlyList<int> OwnRequestIds,
    IReadOnlyList<int> OpenRequestIds,
    TimeSpan RenderLatency);

internal sealed record RequestCallResult(
    string Endpoint,
    HttpStatusCode StatusCode,
    bool Success,
    TimeSpan Elapsed,
    string? RedirectLocation);

internal sealed class MetricsCollector
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> _latencyMs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _success = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _failed = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string operation, bool success, TimeSpan elapsed)
    {
        if (success)
            _success.AddOrUpdate(operation, 1, (_, old) => old + 1);
        else
            _failed.AddOrUpdate(operation, 1, (_, old) => old + 1);

        if (elapsed > TimeSpan.Zero)
            _latencyMs.GetOrAdd(operation, _ => new ConcurrentBag<double>()).Add(elapsed.TotalMilliseconds);
    }

    public void PrintSummary(int durationMinutes)
    {
        Console.WriteLine("\n=== CS Live Help Stress Summary ===");
        var totalSuccess = _success.Values.Sum();
        var totalFail = _failed.Values.Sum();
        var total = totalSuccess + totalFail;
        var rps = total / Math.Max(1.0, durationMinutes * 60.0);

        Console.WriteLine($"Total operations: {total}");
        Console.WriteLine($"Successful: {totalSuccess}");
        Console.WriteLine($"Failed/skipped: {totalFail}");
        Console.WriteLine($"Approx requests/sec: {rps:F2}");

        var keys = _success.Keys.Union(_failed.Keys).OrderBy(x => x).ToList();
        foreach (var key in keys)
        {
            _success.TryGetValue(key, out var ok);
            _failed.TryGetValue(key, out var bad);
            var samples = _latencyMs.TryGetValue(key, out var bag)
                ? bag.OrderBy(x => x).ToArray()
                : Array.Empty<double>();

            var p50 = Percentile(samples, 0.50);
            var p95 = Percentile(samples, 0.95);
            var p99 = Percentile(samples, 0.99);
            var avg = samples.Length == 0 ? 0 : samples.Average();

            Console.WriteLine($"- {key}");
            Console.WriteLine($"  ok={ok}, fail={bad}, avgMs={avg:F1}, p50Ms={p50:F1}, p95Ms={p95:F1}, p99Ms={p99:F1}");
        }
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        index = Math.Clamp(index, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }
}

internal static class StressHelpers
{
    public static int? PickRandomId(IReadOnlyList<int> preferredIds, ICollection<int> fallbackIds)
    {
        if (preferredIds.Count > 0)
            return preferredIds[Random.Shared.Next(preferredIds.Count)];

        if (fallbackIds.Count == 0) return null;
        var asArray = fallbackIds.ToArray();
        return asArray[Random.Shared.Next(asArray.Length)];
    }
}

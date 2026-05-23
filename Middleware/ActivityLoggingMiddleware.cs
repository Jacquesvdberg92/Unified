using System.Diagnostics;
using System.Security.Claims;
using Unified.Models.Logging;
using Unified.Services;

namespace Unified.Middleware;

public class ActivityLoggingMiddleware
{
    private static readonly string[] IgnoredPrefixes = ["/lib/", "/css/", "/js/", "/assets/", "/favicon.ico", "/hubs/"];

    private readonly RequestDelegate _next;

    public ActivityLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IActivityLogQueue queue)
    {
        var requestPath = context.Request.Path.Value ?? string.Empty;

        if (ShouldSkip(context, requestPath))
        {
            await _next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        await _next(context);
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = context.User.Identity?.Name;

        var entry = new ActivityLog
        {
            UserId = userId,
            UserName = userName,
            Action = $"{context.Request.Method} {requestPath}",
            Path = requestPath,
            Method = context.Request.Method,
            StatusCode = context.Response.StatusCode,
            IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            DurationMs = (long)Math.Round(elapsedMs),
            Timestamp = DateTime.UtcNow
        };

        _ = queue.EnqueueAsync(entry);
    }

    private static bool ShouldSkip(HttpContext context, string path)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        foreach (var prefix in IgnoredPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
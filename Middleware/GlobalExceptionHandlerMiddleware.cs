using System.Security.Claims;
using Unified.Data;
using Unified.Models.Logging;

namespace Unified.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            long? errorId = null;
            try
            {
                var log = new ErrorLog
                {
                    UserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Path = context.Request.Path.Value ?? string.Empty,
                    Method = context.Request.Method,
                    ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.ToString(),
                    Timestamp = DateTime.UtcNow
                };

                db.ErrorLogs.Add(log);
                await db.SaveChangesAsync();
                errorId = log.Id;
            }
            catch (Exception persistEx)
            {
                _logger.LogError(persistEx, "Failed to persist unhandled exception log.");
            }

            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            // Too late to swap the response once it has started streaming
            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();

            // AJAX / fetch callers expect a status they can check, not a redirect to an HTML page
            if (context.Request.Headers.XRequestedWith == "XMLHttpRequest"
                || context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Something went wrong. Please try again." });
                return;
            }

            // Pass the error reference and the failing page so the error page can offer
            // "Report this problem" with both filled in
            var query = QueryString.Empty.Add("from", $"{context.Request.Path}{context.Request.QueryString}");
            if (errorId.HasValue)
                query = query.Add("errorId", errorId.Value.ToString());
            context.Response.Redirect($"{context.Request.PathBase}/Home/Error{query}");
        }
    }
}
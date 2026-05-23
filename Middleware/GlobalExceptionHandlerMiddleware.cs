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
            }
            catch (Exception persistEx)
            {
                _logger.LogError(persistEx, "Failed to persist unhandled exception log.");
            }

            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Redirect("/Home/Error");
        }
    }
}
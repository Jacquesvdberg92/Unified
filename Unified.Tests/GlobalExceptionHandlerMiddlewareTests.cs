using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Middleware;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class GlobalExceptionHandlerMiddlewareTests
{
    private static GlobalExceptionHandlerMiddleware Throwing() =>
        new(_ => throw new InvalidOperationException("Boom"), NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

    [Fact]
    public async Task UnhandledException_LogsItAndRedirectsWithReferenceAndPage()
    {
        using var db = DbHelper.CreateInMemory(nameof(UnhandledException_LogsItAndRedirectsWithReferenceAndPage));
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/Brands/Edit/3";
        context.Request.QueryString = new QueryString("?tab=docs");

        await Throwing().InvokeAsync(context, db);

        var log = await db.ErrorLogs.SingleAsync();
        Assert.Equal("System.InvalidOperationException", log.ExceptionType);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal($"/Home/Error?from=%2FBrands%2FEdit%2F3%3Ftab%3Ddocs&errorId={log.Id}", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task UnhandledException_FromFetch_ReturnsJsonInsteadOfRedirect()
    {
        using var db = DbHelper.CreateInMemory(nameof(UnhandledException_FromFetch_ReturnsJsonInsteadOfRedirect));
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/thing";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();

        await Throwing().InvokeAsync(context, db);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Single(db.ErrorLogs);
    }
}

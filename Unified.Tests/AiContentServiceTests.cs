using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Services;

namespace Unified.Tests;

public class AiContentServiceTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request     = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static string Completion(string content, string finishReason = "stop") => JsonSerializer.Serialize(new
    {
        model   = "anthropic/claude-haiku-5.5",
        choices = new[] { new { finish_reason = finishReason, message = new { role = "assistant", content } } }
    });

    private static (AiContentService svc, StubHandler handler) Build(
        HttpStatusCode status, string body, Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI:ApiKey"]            = "test-key",
            ["AI:Model"]             = "anthropic/claude-haiku-5.5",
            ["AI:FallbackModels:0"]  = "openai/gpt-6-luna",
            ["AI:ReasoningEffort"]   = "none",
            ["AI:MaxOutputTokens"]   = "4000"
        };
        foreach (var (k, v) in overrides ?? []) settings[k] = v;

        var config  = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var handler = new StubHandler(status, body);
        return (new AiContentService(new StubFactory(handler), config, NullLogger<AiContentService>.Instance), handler);
    }

    [Fact]
    public async Task ImproveAsync_SendsOpenRouterRequestWithFallbacksAndNoReasoning()
    {
        var (svc, handler) = Build(HttpStatusCode.OK, Completion("<p>Hi there</p>"));

        var result = await svc.ImproveAsync("<p>hi</p>", "make it friendly", "emailTemplate");

        Assert.Equal("<p>Hi there</p>", result);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer test-key", handler.Request.Headers.Authorization!.ToString());
        Assert.Equal("Unified", handler.Request.Headers.GetValues("X-OpenRouter-Title").Single());

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var root = doc.RootElement;
        Assert.Equal(["anthropic/claude-haiku-5.5", "openai/gpt-6-luna"],
            root.GetProperty("models").EnumerateArray().Select(m => m.GetString()!).ToArray());
        Assert.False(root.TryGetProperty("model", out _));
        Assert.Equal("none", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal(4000, root.GetProperty("max_tokens").GetInt32());
        Assert.Contains("customer-facing emails", root.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ImproveAsync_WithoutFallbacks_SendsSingleModel()
    {
        var (svc, handler) = Build(HttpStatusCode.OK, Completion("ok"),
            new() { ["AI:FallbackModels:0"] = null, ["AI:ReasoningEffort"] = "" });

        await svc.ImproveAsync("text", "", "update");

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("anthropic/claude-haiku-5.5", doc.RootElement.GetProperty("model").GetString());
        Assert.False(doc.RootElement.TryGetProperty("models", out _));
        Assert.False(doc.RootElement.TryGetProperty("reasoning", out _));
    }

    [Fact]
    public async Task ImproveAsync_MasksEmbeddedImagesAndRestoresThem()
    {
        const string image = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB";
        var (svc, handler) = Build(HttpStatusCode.OK,
            Completion("<p>Better</p><img src=\"IMAGE_PLACEHOLDER_1\"><img src=\"IMAGE_PLACEHOLDER_2\">"));

        var result = await svc.ImproveAsync($"<p>x</p><img src=\"{image}\"><img src=\"{image}2\">", "", "emailTemplate");

        Assert.DoesNotContain("base64", handler.RequestBody);
        Assert.Equal($"<p>Better</p><img src=\"{image}\"><img src=\"{image}2\">", result);
    }

    [Fact]
    public async Task ImproveAsync_StripsMarkdownCodeFence()
    {
        var (svc, _) = Build(HttpStatusCode.OK, Completion("```html\n<p>Clean</p>\n```"));

        Assert.Equal("<p>Clean</p>", await svc.ImproveAsync("<p>x</p>", "", "update"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "rejected the API key")]
    [InlineData(HttpStatusCode.PaymentRequired, "out of credits")]
    [InlineData(HttpStatusCode.TooManyRequests, "busy")]
    [InlineData(HttpStatusCode.BadGateway, "failed (502)")]
    public async Task ImproveAsync_MapsErrorStatusToFriendlyMessage(HttpStatusCode status, string expected)
    {
        var (svc, _) = Build(status, """{"error":{"code":1,"message":"provider detail"}}""");

        var ex = await Assert.ThrowsAsync<AiContentException>(() => svc.ImproveAsync("text", "", "update"));
        Assert.Contains(expected, ex.Message);
        Assert.DoesNotContain("provider detail", ex.Message);
    }

    [Fact]
    public async Task ImproveAsync_TruncatedResponse_Throws()
    {
        var (svc, _) = Build(HttpStatusCode.OK, Completion("<p>Half", finishReason: "length"));

        var ex = await Assert.ThrowsAsync<AiContentException>(() => svc.ImproveAsync("text", "", "update"));
        Assert.Contains("cut off", ex.Message);
    }

    [Fact]
    public async Task ImproveAsync_MissingKey_ThrowsWithoutCallingApi()
    {
        var (svc, handler) = Build(HttpStatusCode.OK, Completion("x"), new() { ["AI:ApiKey"] = "" });

        var ex = await Assert.ThrowsAsync<AiContentException>(() => svc.ImproveAsync("text", "", "update"));
        Assert.Contains("not configured", ex.Message);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task ImproveAsync_InputTooLong_ThrowsWithoutCallingApi()
    {
        var (svc, handler) = Build(HttpStatusCode.OK, Completion("x"));

        await Assert.ThrowsAsync<AiContentException>(() =>
            svc.ImproveAsync(new string('a', AiContentService.MaxInputChars + 1), "", "update"));
        Assert.Null(handler.Request);
    }
}

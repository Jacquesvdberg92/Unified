using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unified.Services;

/// <summary>Raised for AI failures whose message is safe to show to the user.</summary>
public class AiContentException(string message) : Exception(message);

/// <summary>
/// Rewrites update and email-template content through OpenRouter's OpenAI-compatible
/// chat completions API. Configured under the "AI" section of appsettings.
/// </summary>
public class AiContentService
{
    public const string HttpClientName  = "OpenRouter";
    public const string RateLimitPolicy = "ai";

    public const int MaxInputChars  = 30_000;
    public const int MaxPromptChars = 1_000;

    private const string DefaultEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    private const string DefaultModel    = "anthropic/claude-haiku-5.5";

    // Pasted images arrive as base64 data URIs; sending them would burn tokens and
    // the model would have to echo them back. Swap them for placeholders instead.
    private static readonly Regex DataUriSrc = new("src=\"(data:[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CodeFence  = new(@"^```[a-zA-Z]*\s*\n(?<body>[\s\S]*?)\n?```\s*$", RegexOptions.Compiled);

    private const string FormatRules =
        " The content is HTML from a rich-text editor. Return HTML only, using simple tags such as <p>, <br>, <strong>, <em>, <u>, <ul>, <ol>, <li> and <a>." +
        " Do not wrap the answer in markdown or code fences, and do not add commentary before or after it." +
        " Keep every {{Placeholder:Token}} exactly as written, and keep every <img> tag and its src value unchanged.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiContentService> _logger;

    public AiContentService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<AiContentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> ImproveAsync(string inputText, string userPrompt, string context, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["AI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiContentException("AI is not configured. Set AI:ApiKey to an OpenRouter API key.");

        if (inputText.Length > MaxInputChars)
            throw new AiContentException($"This content is too long for AI improve (limit {MaxInputChars:N0} characters).");
        if (userPrompt.Length > MaxPromptChars)
            throw new AiContentException($"The prompt is too long (limit {MaxPromptChars:N0} characters).");

        var endpoint        = _configuration["AI:Endpoint"] is { Length: > 0 } e ? e : DefaultEndpoint;
        var model           = _configuration["AI:Model"] is { Length: > 0 } m ? m : DefaultModel;
        var fallbackModels  = _configuration.GetSection("AI:FallbackModels").Get<string[]>() ?? [];
        var reasoningEffort = _configuration["AI:ReasoningEffort"];
        var maxOutputTokens = _configuration.GetValue("AI:MaxOutputTokens", 4000);

        var systemInstruction = context?.ToLowerInvariant() switch
        {
            "update" => "You rewrite internal team updates to be friendly, clear, and professional. Keep the meaning intact and improve grammar and tone.",
            "emailtemplate" => "You are an expert writer of professional customer-facing emails. Improve the email for clarity, grammar, warmth, and a professional tone while preserving its structure, facts, and intent.",
            _ => "You improve writing for clarity, grammar, and professional tone."
        } + FormatRules;

        var images = new List<string>();
        var maskedInput = DataUriSrc.Replace(inputText, match =>
        {
            images.Add(match.Groups[1].Value);
            return $"src=\"IMAGE_PLACEHOLDER_{images.Count}\"";
        });

        var prompt = $"User instruction: {userPrompt}\n\nOriginal content:\n{maskedInput}";

        // OpenRouter tries each entry of "models" in order when one fails or is unavailable
        var payload = new Dictionary<string, object>
        {
            ["messages"] = new object[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = prompt }
            },
            ["max_tokens"] = maxOutputTokens
        };
        var models = new[] { model }.Concat(fallbackModels.Where(f => !string.IsNullOrWhiteSpace(f)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (models.Length > 1)
            payload["models"] = models;
        else
            payload["model"] = model;
        // Rewriting doesn't benefit from reasoning, and reasoning tokens are billed as output
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
            payload["reasoning"] = new { effort = reasoningEffort };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("X-OpenRouter-Title", "Unified");

        var client = _httpClientFactory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "AI request to {Endpoint} failed", endpoint);
            throw new AiContentException("Couldn't reach the AI service. Please try again.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI request failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
                throw new AiContentException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "The AI service rejected the API key. Check AI:ApiKey.",
                    HttpStatusCode.PaymentRequired => "The AI account is out of credits.",
                    HttpStatusCode.TooManyRequests => "The AI service is busy. Please try again in a moment.",
                    _ => $"The AI request failed ({(int)response.StatusCode}). Please try again."
                });
            }

            string? result, servedBy, finishReason;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root   = doc.RootElement;
                var choice = root.GetProperty("choices")[0];
                result       = choice.GetProperty("message").GetProperty("content").GetString();
                finishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
                servedBy     = root.TryGetProperty("model", out var mo) ? mo.GetString() : null;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                _logger.LogWarning(ex, "Unexpected AI response: {Body}", body);
                throw new AiContentException("The AI service returned an unexpected response.");
            }

            _logger.LogInformation("AI improve ({Context}) served by {Model}, finish_reason={FinishReason}", context, servedBy, finishReason);

            if (finishReason == "length")
                throw new AiContentException("The AI response was cut off. Try shorter content.");
            if (string.IsNullOrWhiteSpace(result))
                throw new AiContentException("The AI service returned an empty response.");

            result = result.Trim();
            var fenced = CodeFence.Match(result);
            if (fenced.Success)
                result = fenced.Groups["body"].Value.Trim();

            for (var i = images.Count; i >= 1; i--)
                result = result.Replace($"IMAGE_PLACEHOLDER_{i}", images[i - 1]);

            return result;
        }
    }
}

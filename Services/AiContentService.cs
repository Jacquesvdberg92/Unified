using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Unified.Services;

public class AiContentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AiContentService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<string> ImproveAsync(string inputText, string userPrompt, string context)
    {
        var apiKey = _configuration["AI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI API key is not configured.");

        var endpoint = _configuration["AI:Endpoint"] ?? "https://models.inference.ai.azure.com/chat/completions";
        var configuredModel = _configuration["AI:Model"] ?? "openai/gpt-4o-mini";

        var modelCandidates = new List<string>
        {
            configuredModel,
            "openai/gpt-4o-mini",
            "gpt-4o-mini",
            "openai/gpt-4.1-mini",
            "gpt-4.1-mini"
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var systemInstruction = context?.ToLowerInvariant() switch
        {
            "update" => "You rewrite internal team updates to be friendly, clear, and professional. Keep meaning intact, improve grammar and tone, and return only the rewritten content.",
            "emailtemplate" => "You improve email template content for clarity, grammar, and professional tone while preserving structure and intent. Return only the improved content.",
            _ => "You improve writing for clarity, grammar, and professional tone. Return only the improved content."
        };

        var prompt = $"User instruction: {userPrompt}\n\nOriginal content:\n{inputText}";

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        string? lastError = null;

        foreach (var model in modelCandidates)
        {
            var payload = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = systemInstruction },
                    new { role = "user", content = prompt }
                },
                temperature = 0.4
            };

            using var response = await client.PostAsync(
                endpoint,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var result = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return result?.Trim() ?? string.Empty;
            }

            lastError = $"AI request failed: {(int)response.StatusCode} {response.ReasonPhrase} - {body}";

            if (!body.Contains("unknown_model", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(lastError);
        }

        throw new InvalidOperationException(lastError ?? "AI request failed.");
    }
}

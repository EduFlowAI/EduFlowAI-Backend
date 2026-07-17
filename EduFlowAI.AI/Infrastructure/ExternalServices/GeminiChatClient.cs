using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Infrastructure.ExternalServices;

/// <summary>
/// Single Gemini generateContent client shared by the RAG answer service and the intent
/// classifier. Builds the endpoint from <see cref="GeminiOptions"/> (BaseUrl + ChatModel), so
/// changing configuration re-points every generation call in one place.
/// </summary>
public class GeminiChatClient : IGeminiChatClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiChatClient(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    // Transient statuses worth retrying: 429 (rate limit) and any 5xx (e.g. 503 "high demand").
    private const int MaxAttempts = 3;

    public async Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        var requestUrl = $"{_options.BaseUrl.TrimEnd('/')}/models/{_options.ChatModel}:generateContent?key={_options.ApiKey}";

        var payload = new GeminiGenerateRequest
        {
            SystemInstruction = new GeminiContent { Parts = new[] { new GeminiPart { Text = systemPrompt } } },
            Contents = new[] { new GeminiContent { Parts = new[] { new GeminiPart { Text = userMessage } } } }
        };

        for (var attempt = 1; ; attempt++)
        {
            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(cancellationToken: cancellationToken);

                // FirstOrDefault guards against an empty candidates/parts array (avoids IndexOutOfRange).
                // An empty string is a valid "no answer" signal the caller decides how to handle.
                return body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
            }

            var status = (int)response.StatusCode;
            var isTransient = status == 429 || status >= 500;

            if (isTransient && attempt < MaxAttempts)
            {
                // Back off (2s, 4s) to ride out a rate-limit window or a demand spike.
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                continue;
            }

            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            if (detail.Length > 300)
                detail = detail.Substring(0, 300);
            throw new HttpRequestException($"Gemini generateContent failed with HTTP {status} for model '{_options.ChatModel}'. {detail}");
        }
    }
}

// --- Gemini generateContent request/response contract (single definition for the whole module) ---

file sealed class GeminiGenerateRequest
{
    [JsonPropertyName("system_instruction")]
    public GeminiContent? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
}

file sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
}

file sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

file sealed class GeminiGenerateResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; set; }
}

file sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

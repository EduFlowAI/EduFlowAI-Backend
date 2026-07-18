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

public class GeminiChatClient : IGeminiChatClient
{
    private const int MaxAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiChatClient(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        var request = new GeminiGenerateRequest
        {
            SystemInstruction = new GeminiContent { Parts = new[] { GeminiPart.FromText(systemPrompt) } },
            Contents = new[] { new GeminiContent { Parts = new[] { GeminiPart.FromText(userMessage) } } },
            GenerationConfig = new GeminiGenerationConfig { Temperature = _options.ChatTemperature }
        };

        return SendAsync(request, cancellationToken);
    }

    public Task<string> GenerateJsonAsync(string systemPrompt, string userMessage, object responseSchema, CancellationToken cancellationToken = default)
    {
        var request = new GeminiGenerateRequest
        {
            SystemInstruction = new GeminiContent { Parts = new[] { GeminiPart.FromText(systemPrompt) } },
            Contents = new[] { new GeminiContent { Parts = new[] { GeminiPart.FromText(userMessage) } } },
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0,
                ResponseMimeType = "application/json",
                ResponseSchema = responseSchema
            }
        };

        return SendAsync(request, cancellationToken);
    }

    public Task<string> GenerateFromDocumentAsync(string instruction, byte[] document, string mimeType, CancellationToken cancellationToken = default)
    {
        if (document == null || document.Length == 0)
            throw new ArgumentException("Document content cannot be empty.", nameof(document));

        var request = new GeminiGenerateRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Parts = new[]
                    {
                        GeminiPart.FromInlineData(mimeType, Convert.ToBase64String(document)),
                        GeminiPart.FromText(instruction)
                    }
                }
            },
            GenerationConfig = new GeminiGenerationConfig { Temperature = 0 }
        };

        return SendAsync(request, cancellationToken);
    }

    private async Task<string> SendAsync(GeminiGenerateRequest payload, CancellationToken cancellationToken)
    {
        var requestUrl = $"{_options.BaseUrl.TrimEnd('/')}/models/{_options.ChatModel}:generateContent?key={_options.ApiKey}";

        for (var attempt = 1; ; attempt++)
        {
            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(cancellationToken: cancellationToken);

                return body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
            }

            var status = (int)response.StatusCode;
            var isTransient = status == 429 || status >= 500;

            if (isTransient && attempt < MaxAttempts)
            {
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

internal sealed class GeminiGenerateRequest
{
    [JsonPropertyName("system_instruction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiContent? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();

    [JsonPropertyName("generationConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

internal sealed class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    [JsonPropertyName("responseMimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseMimeType { get; set; }

    [JsonPropertyName("responseSchema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ResponseSchema { get; set; }
}

internal sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
}

internal sealed class GeminiPart
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("inline_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiInlineData? InlineData { get; set; }

    public static GeminiPart FromText(string text) => new() { Text = text };

    public static GeminiPart FromInlineData(string mimeType, string base64Data)
        => new() { InlineData = new GeminiInlineData { MimeType = mimeType, Data = base64Data } };
}

internal sealed class GeminiInlineData
{
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}

internal sealed class GeminiGenerateResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; set; }
}

internal sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Application.Services;

public class IntentClassifierService : IIntentClassifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] ValidIntents = { "knowledge", "status", "recommendation" };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<IntentClassifierService> _logger;

    public IntentClassifierService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<IntentClassifierService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IntentClassificationDto> ClassifyAsync(
        string userQuestion,
        List<ConversationTurnDto> context,
        CancellationToken cancellationToken = default)
    {
        var contextText = context.Any()
            ? string.Join("\n", context.TakeLast(3).Select(t => $"Q: {t.Question}\nA: {t.Answer}"))
            : "No prior conversation history.";

        var systemPrompt = @"
You are an intent classifier for Information Technology Institute chatbot:
Classify the user's question into ONE of these categories:
1. 'knowledge' - Questions about admission requirements, eligibility, processes, timelines, FAQs
2. 'status' - Questions about application status, admission decisions, progress
3. 'recommendation' - Questions about program recommendations, track suggestions

Respond ONLY with valid JSON in this exact format:
{
  ""primaryIntent"": ""knowledge"" | ""status"" | ""recommendation"",
  ""confidence"": 0.95,
  ""requiresClarification"": false
}

Confidence should be 0.0 to 1.0.
";

        var userMessage = $@"
Conversation context:
{contextText}

Current question: {userQuestion}

Classify this question. Respond ONLY with JSON.
";

        var requestUrl = $"{_options.BaseUrl.TrimEnd('/')}/models/gemini-2.5-flash:generateContent?key={_options.ApiKey}";

        var payload = new GeminiChatRequestDto
        {
            Contents = new[] { new GeminiContentDto { Parts = new[] { new GeminiPartDto { Text = userMessage } } } },
            SystemInstruction = new GeminiSystemInstructionDto { Parts = new[] { new GeminiPartDto { Text = systemPrompt } } }
        };

        var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiChatResponseDto>(cancellationToken: cancellationToken);

        // FirstOrDefault guards against an empty candidates/parts array (avoids IndexOutOfRange).
        var responseText = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;

        if (!TryParseIntentResponse(responseText, out var classification))
        {
            _logger.LogWarning(
                "Intent classifier could not parse a valid intent from the model response; " +
                "falling back to 'knowledge'. Raw response: {RawResponse}",
                responseText);
        }

        return classification;
    }

    /// <summary>
    /// Parses the model's raw text into an intent classification. Tolerates Markdown code
    /// fences and surrounding prose, matches JSON property names case-insensitively, validates
    /// the intent value, clamps confidence to [0,1], and derives RoutedTo. Returns false (with a
    /// safe 'knowledge' fallback in <paramref name="result"/>) when no valid intent can be read.
    /// </summary>
    internal static bool TryParseIntentResponse(string? responseText, out IntentClassificationDto result)
    {
        result = KnowledgeFallback();

        var json = ExtractJsonObject(responseText);
        if (json is null)
            return false;

        IntentClassificationDto? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<IntentClassificationDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null)
            return false;

        var intent = (parsed.PrimaryIntent ?? string.Empty).Trim().ToLowerInvariant();
        if (Array.IndexOf(ValidIntents, intent) < 0)
            return false;

        parsed.PrimaryIntent = intent;
        parsed.Confidence = Math.Clamp(parsed.Confidence, 0m, 1m);
        parsed.RoutedTo = RouteFor(intent);
        result = parsed;
        return true;
    }

    private static string RouteFor(string intent) => intent switch
    {
        "status" => "status_agent",
        "recommendation" => "recommendations_agent",
        _ => "knowledge_rag"
    };

    private static IntentClassificationDto KnowledgeFallback() => new()
    {
        PrimaryIntent = "knowledge",
        Confidence = 0.5m,
        RoutedTo = "knowledge_rag",
        RequiresClarification = true
    };

    /// <summary>Extracts the outermost JSON object from text that may include code fences or prose.</summary>
    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var t = text.Trim();

        // Strip a leading ```json / ``` fence and its closing ``` if present.
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = t.IndexOf('\n');
            if (firstNewline >= 0)
                t = t.Substring(firstNewline + 1);

            var closingFence = t.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
                t = t.Substring(0, closingFence);

            t = t.Trim();
        }

        var start = t.IndexOf('{');
        var end = t.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        return t.Substring(start, end - start + 1);
    }
}

// DTOs for Gemini API calls (raw request/response shape Gemini expects)
public class GeminiChatRequestDto
{
    [JsonPropertyName("system_instruction")]
    public GeminiSystemInstructionDto? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public GeminiContentDto[]? Contents { get; set; }
}

public class GeminiSystemInstructionDto
{
    [JsonPropertyName("parts")]
    public GeminiPartDto[]? Parts { get; set; }
}

public class GeminiContentDto
{
    [JsonPropertyName("parts")]
    public GeminiPartDto[]? Parts { get; set; }
}

public class GeminiPartDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiChatResponseDto
{
    [JsonPropertyName("candidates")]
    public GeminiCandidateDto[]? Candidates { get; set; }
}

public class GeminiCandidateDto
{
    [JsonPropertyName("content")]
    public GeminiContentDto? Content { get; set; }
}

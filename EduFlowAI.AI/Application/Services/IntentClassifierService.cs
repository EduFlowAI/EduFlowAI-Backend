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
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Application.Services;

public class IntentClassifierService : IIntentClassifier
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public IntentClassifierService(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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
        var responseText = result?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? string.Empty;

        try
        {
            var classification = JsonSerializer.Deserialize<IntentClassificationDto>(responseText);
            if (classification == null)
                throw new InvalidOperationException("Failed to deserialize intent classification");

            classification.RoutedTo = classification.PrimaryIntent switch
            {
                "knowledge" => "knowledge_rag",
                "status" => "status_agent",
                "recommendation" => "recommendations_agent",
                _ => "knowledge_rag"
            };

            return classification;
        }
        catch
        {
            // Fallback: assume knowledge question if Gemini's response wasn't valid JSON
            return new IntentClassificationDto
            {
                PrimaryIntent = "knowledge",
                Confidence = 0.5m,
                RoutedTo = "knowledge_rag",
                RequiresClarification = true
            };
        }
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
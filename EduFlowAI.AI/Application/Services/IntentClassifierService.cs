using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Application.Services;

public class IntentClassifierService : IIntentRouter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] ValidIntents = { "knowledge", "status", "recommendation" };

    // The enum constraint makes an out-of-vocabulary intent impossible.
    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            primaryIntent = new { type = "STRING", @enum = new[] { "knowledge", "status", "recommendation" } },
            confidence = new { type = "NUMBER" },
            reason = new { type = "STRING" }
        },
        required = new[] { "primaryIntent", "confidence" }
    };

    private const string SystemPrompt = """
You are the intent router for the Information Technology Institute (ITI) admission assistant.
Classify the user's message into exactly one intent.

Decide by what the user WANTS, never by which keywords appear:

knowledge - objective, public facts about ITI: programs, which tracks exist, what a track covers,
admission requirements, eligibility, grades, fees, dates, locations, the selection process and how
to apply. The answer is identical for every applicant and comes from official documents. Listing,
describing or explaining things belongs here.

status - the state of THIS user's own application or records: whether they were accepted, how far
their application has progressed, whether their documents were received.

recommendation - asks the assistant to CHOOSE or ADVISE which option suits THIS user personally,
based on their background, interests, skills or preferences.

Critical rule: asking WHICH TRACKS EXIST is knowledge. Asking WHICH TRACK SUITS ME is
recommendation. The word "track" never decides the intent by itself - the user's goal does.

Set confidence honestly. Use a low value when the message is vague, very short, or could
reasonably belong to more than one intent.

Examples:
"Which are available tracks?" -> knowledge
"What tracks does ITI offer?" -> knowledge
"ما هي المسارات المتاحة؟" -> knowledge
"Tell me about the .NET track" -> knowledge
"What is the minimum graduation grade?" -> knowledge
"How do I apply?" -> knowledge
"Which track should I choose?" -> recommendation
"Which track fits someone with a design background?" -> recommendation
"ما هو المسار المناسب لي؟" -> recommendation
"What is my application status?" -> status
"هل تم قبولي؟" -> status
"Did you receive my documents?" -> status
""";

    private readonly IGeminiChatClient _chatClient;
    private readonly IntentClassificationOptions _options;
    private readonly ILogger<IntentClassifierService> _logger;

    public IntentClassifierService(
        IGeminiChatClient chatClient,
        IOptions<IntentClassificationOptions> options,
        ILogger<IntentClassifierService> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _options = options?.Value ?? new IntentClassificationOptions();
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

        var userMessage = $"""
Conversation so far:
{contextText}

Message to classify:
{userQuestion}
""";

        var responseText = await _chatClient.GenerateJsonAsync(SystemPrompt, userMessage, ResponseSchema, cancellationToken);

        if (!TryParseIntentResponse(responseText, out var classification))
        {
            // Our problem, not the user's - answer anyway rather than forcing a clarification loop.
            _logger.LogWarning(
                "Intent classifier could not parse a valid intent; falling back to 'knowledge'. Raw response: {RawResponse}",
                responseText);
            return classification;
        }

        if (classification.Confidence < _options.MinConfidence)
        {
            _logger.LogInformation(
                "Intent '{Intent}' below confidence threshold ({Confidence} < {Threshold}); asking the user to clarify.",
                classification.PrimaryIntent, classification.Confidence, _options.MinConfidence);
            classification.RequiresClarification = true;
        }

        return classification;
    }

    // Tolerates Markdown fences and stray prose in case a model ignores the schema.
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
        parsed.RequiresClarification = false;
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
        RequiresClarification = false
    };

    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var t = text.Trim();

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

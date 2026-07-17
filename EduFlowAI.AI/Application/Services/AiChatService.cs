using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Application.Services;

public class AiChatService
{
    private readonly KnowledgeBaseRetrievalService _retrievalService;
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public AiChatService(
        KnowledgeBaseRetrievalService retrievalService,
        HttpClient httpClient,
        IOptions<GeminiOptions> options)
    {
        _retrievalService = retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<string> AnswerWithContextAsync(
        string userQuestion,
        string language,   // "ar" or "en" — now passed in, not detected here
        CancellationToken cancellationToken = default)
    {
        //  Fetch top relevant context blocks from the knowledge base
        var contextChunks = await _retrievalService.RetrieveContextAsync(userQuestion, limit: 5, cancellationToken);

        //  Build the prompt injecting context as the foundation of ground truth
        var contextText = contextChunks.Count > 0
            ? string.Join("\n\n---\n\n", contextChunks)
            : "No specific reference documents found.";

        var languageInstruction = language == "ar"
            ? "Respond in Arabic only."
            : "Respond in English only.";

        var systemPrompt =
            "You are an expert academic AI tutor for EduFlowAI. Your task is to answer the user's question accurately.\n" +
            $"{languageInstruction}\n" +
            "Base your answer STRICTLY on the context provided below. If the context does not contain enough information to answer the question, " +
            "politely tell the user that you don't know based on the uploaded materials.\n\n" +
            $"[DOCUMENTS CONTEXT]:\n{contextText}\n\n";

        //  Structure the request URL to target Gemini 1.5/2.5 Flash
        var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_options.ApiKey}";

        var payload = new GeminiRequestPayload
        {
            SystemInstruction = new SystemInstruction { Parts = new[] { new Part { Text = systemPrompt } } },
            Contents = new[] { new Content { Parts = new[] { new Part { Text = userQuestion } } } }
        };

        //  Dispatch post request to Gemini API
        var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<GeminiResponsePayload>(cancellationToken: cancellationToken);

        //  Extract and return the markdown text response
        var answer = responseBody?.Candidates?[0]?.Content?.Parts?[0]?.Text;
        return answer ?? "I'm sorry, I couldn't generate an answer from the material provided.";
    }
}

#region Gemini REST API Contract Mapping

public class GeminiRequestPayload
{
    [JsonPropertyName("system_instruction")]
    public SystemInstruction? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public Content[] Contents { get; set; } = Array.Empty<Content>();
}

public class SystemInstruction
{
    [JsonPropertyName("parts")]
    public Part[] Parts { get; set; } = Array.Empty<Part>();
}

public class Content
{
    [JsonPropertyName("parts")]
    public Part[] Parts { get; set; } = Array.Empty<Part>();
}

public class Part
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiResponsePayload
{
    [JsonPropertyName("candidates")]
    public Candidate[]? Candidates { get; set; }
}

public class Candidate
{
    [JsonPropertyName("content")]
    public Content? Content { get; set; }
}

#endregion
using System;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Application.Services;

public class AiChatService
{
    private readonly KnowledgeBaseRetrievalService _retrievalService;
    private readonly IGeminiChatClient _chatClient;

    public AiChatService(KnowledgeBaseRetrievalService retrievalService, IGeminiChatClient chatClient)
    {
        _retrievalService = retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    public async Task<string> AnswerWithContextAsync(
        string userQuestion,
        string language,   // "ar" or "en" — detected by the controller and passed in
        CancellationToken cancellationToken = default)
    {
        // Fetch top relevant context blocks from the knowledge base.
        var contextChunks = await _retrievalService.RetrieveContextAsync(userQuestion, limit: 5, cancellationToken);

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

        var answer = await _chatClient.GenerateAsync(systemPrompt, userQuestion, cancellationToken);

        return string.IsNullOrWhiteSpace(answer)
            ? "I'm sorry, I couldn't generate an answer from the material provided."
            : answer;
    }
}

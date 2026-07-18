using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
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

    public async Task<RagAnswerDto> AnswerWithContextAsync(
        string userQuestion,
        string language,   // "ar" or "en" — detected by the controller and passed in
        CancellationToken cancellationToken = default)
    {
        var chunks = await _retrievalService.RetrieveContextAsync(userQuestion, limit: 5, cancellationToken);

        if (chunks.Count == 0)
        {
            return new RagAnswerDto
            {
                Answer = language == "ar"
                    ? "لا توجد مستندات في قاعدة المعرفة للإجابة على سؤالك بعد."
                    : "There are no documents in the knowledge base yet, so I can't answer that."
            };
        }

        var sources = chunks
            .Select(c => c.SourceTitle)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Label every excerpt with its document. Without this the model receives anonymous text
        // and cannot answer questions about what the knowledge base covers.
        var contextText = string.Join("\n\n---\n\n",
            chunks.Select(c => $"[Source document: {c.SourceTitle}]\n{c.Content}"));

        var languageInstruction = language == "ar"
            ? "Respond in Arabic only."
            : "Respond in English only.";

        var systemPrompt = $"""
You are the official assistant for the Information Technology Institute (ITI) admission platform.
{languageInstruction}

Answer strictly from the excerpts below. Never invent facts, numbers, dates or names.

How to use the excerpts:
- Each excerpt is labelled with the document it came from. Those documents are what the knowledge
  base currently holds, and the excerpts are genuine ITI material.
- When asked what exists or is available (tracks, programs, requirements), answer from what the
  documents actually cover, and name it specifically. If the excerpts describe one program or
  track, say that this is the one available in the current materials rather than claiming nothing
  was found.
- Say what you DO know first. Only mention that something is missing if the excerpts genuinely do
  not address it, and be specific about what is missing.
- If the excerpts truly do not answer the question, say so plainly and suggest what to ask instead.

Formatting: answer directly and concisely in plain prose. Do not mention "excerpts", "context" or
"documents provided" — speak naturally, as the institute's assistant.

[KNOWLEDGE BASE EXCERPTS]
{contextText}
""";

        var answer = await _chatClient.GenerateAsync(systemPrompt, userQuestion, cancellationToken);

        return new RagAnswerDto
        {
            Answer = string.IsNullOrWhiteSpace(answer)
                ? "I'm sorry, I couldn't generate an answer from the material provided."
                : answer,
            Sources = sources
        };
    }
}

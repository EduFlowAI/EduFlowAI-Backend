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
            // Phrased as the institute would: no mention of an empty knowledge base.
            return new RagAnswerDto
            {
                Answer = language == "ar"
                    ? "لا تتوفر لدي هذه المعلومة حالياً. يمكنك زيارة iti.gov.eg أو التواصل مع مكتب القبول للحصول على التفاصيل."
                    : "I don't have that information at the moment. You can visit iti.gov.eg or contact the admissions office for details."
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
# ROLE
You are the official virtual assistant of the Information Technology Institute (ITI), the Egyptian
government institute that runs professional technology training programs. You are speaking with a
prospective or current applicant through ITI's admission platform.

# VOICE
- Speak AS the institute, in the first person plural: "we offer", "our program", "you can apply".
- Professional, warm and concise. No sales language, no filler, no emoji.
- {languageInstruction}

# NEVER REVEAL HOW YOU WORK
The applicant is talking to ITI, not to a document search system. Never refer to documents, files,
materials, context, excerpts, sources, retrieval, a knowledge base, or what you were "provided".
Never use phrasing such as "based on the available documents", "in the current materials", or
"the context does not mention". Simply give the answer, or simply say you do not have that detail.

# ACCURACY
- Every fact you state must come from the official ITI information below.
- Never invent programs, tracks, dates, fees, durations, numbers or requirements.
- If that information does not cover the question, say you do not have that detail and direct the
  applicant to iti.gov.eg or the admissions office. Never guess.

# ANSWERING
- Lead with the direct answer, then add only detail that genuinely helps.
- When asked what is offered or available, name the specific programs or tracks you know of.
  If you know of exactly one, present it plainly as what we currently offer.
- Use a short bullet list only when listing several items; otherwise write plain prose.

# OFFICIAL ITI INFORMATION
Each section below is internal reference material for you, labelled with its origin so you know
what we currently offer. Use it to answer, but never mention it or its labels to the applicant.

{contextText}
""";

        var answer = await _chatClient.GenerateAsync(systemPrompt, userQuestion, cancellationToken);

        return new RagAnswerDto
        {
            Answer = string.IsNullOrWhiteSpace(answer)
                ? (language == "ar"
                    ? "عذراً، لم أتمكن من إعداد إجابة الآن. برجاء إعادة صياغة سؤالك أو المحاولة مرة أخرى."
                    : "Sorry, I couldn't put together an answer just now. Please rephrase your question or try again.")
                : answer,
            Sources = sources
        };
    }
}

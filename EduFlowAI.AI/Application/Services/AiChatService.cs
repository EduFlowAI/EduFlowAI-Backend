using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Application.Services;

public class AiChatService : IRagAnswerService
{
    private readonly IKnowledgeRetrievalService _retrievalService;
    private readonly IGeminiChatClient _chatClient;

    public AiChatService(IKnowledgeRetrievalService retrievalService, IGeminiChatClient chatClient)
    {
        _retrievalService = retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    public async Task<RagAnswerDto> AnswerWithContextAsync(
        string userQuestion,
        IReadOnlyList<ConversationTurnDto> context,
        string language,
        CancellationToken cancellationToken = default)
    {
        // A follow-up like "what about its prerequisites?" embeds poorly on its own, so retrieval
        // runs on the recent turns joined with the question - the vector then reflects the topic.
        var retrievalQuery = BuildRetrievalQuery(userQuestion, context);
        var chunks = await _retrievalService.RetrieveContextAsync(retrievalQuery, limit: 5, cancellationToken);

        if (chunks.Count == 0)
        {
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

        // Labelling each excerpt with its document is what lets the assistant answer
        // "what do you offer" questions instead of seeing anonymous text.
        var contextText = string.Join("\n\n---\n\n",
            chunks.Select(c => $"[Source document: {c.SourceTitle}]\n{c.Content}"));

        var languageInstruction = language == "ar"
            ? "Respond in Arabic only."
            : "Respond in English only.";

        // Prior turns let the model resolve references ("its", "that track") to what was just
        // discussed. Framed as dialogue, never as documents, so the guardrail below still holds.
        var conversationSection = context.Count == 0
            ? string.Empty
            : $"""

# CONVERSATION SO FAR
This is the dialogue with the applicant up to now. Use it only to understand what the current
question refers to (for example, what "it" or "that track" means). Do not repeat it back.

{RenderTurns(context)}
""";

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
{conversationSection}
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

    // The last couple of turns joined with the question, so the retrieval embedding carries the
    // topic the follow-up depends on. Empty context leaves the question untouched.
    private static string BuildRetrievalQuery(string userQuestion, IReadOnlyList<ConversationTurnDto> context)
    {
        if (context.Count == 0)
            return userQuestion;

        var recent = context.TakeLast(2);
        return $"{RenderTurns(recent)}\n{userQuestion}";
    }

    private static string RenderTurns(IEnumerable<ConversationTurnDto> turns) =>
        string.Join("\n", turns.Select(t => $"Q: {t.Question}\nA: {Trim(t.Answer)}"));

    // A stale, long prior answer shouldn't dominate the retrieval vector or bloat the prompt.
    private static string Trim(string answer)
    {
        answer = answer.Trim();
        return answer.Length <= 200 ? answer : answer.Substring(0, 200);
    }
}

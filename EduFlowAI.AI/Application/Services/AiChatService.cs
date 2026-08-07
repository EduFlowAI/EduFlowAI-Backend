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
        // Mask national IDs and other long digit runs before any user text reaches the model.
        userQuestion = InputSanitizerService.MaskSensitiveNumbers(userQuestion);

        // A bare follow-up embeds poorly, so retrieval runs on recent turns joined with the question.
        var retrievalQuery = BuildRetrievalQuery(userQuestion, context);
        var chunks = await _retrievalService.RetrieveContextAsync(retrievalQuery, limit: 5, cancellationToken);

        var sources = chunks
            .Select(c => c.SourceTitle)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var languageName = language == "ar" ? "Arabic" : "English";

        // Prior turns let the model resolve references ("its", "that track"), framed as dialogue not documents so the guardrail holds.
        var conversationSection = context.Count == 0
            ? string.Empty
            : $"""

# CONVERSATION SO FAR
This is the dialogue with the applicant up to now. Use it only to understand what the current
question refers to (for example, what "it" or "that track" means). Do not repeat it back.

{RenderTurns(context)}
""";

        // Only present when retrieval found something. A greeting, a thank-you or an off-book question
        // has no reference material - the assistant then chats naturally without inventing ITI facts,
        // instead of returning a blunt "I don't have that".
        var knowledgeSection = chunks.Count == 0
            ? string.Empty
            : $"""

# OFFICIAL ITI INFORMATION
Each section below is internal reference material for you, labelled with its origin so you know
what we currently offer. Use it to answer, but never mention it or its labels to the applicant.

{string.Join("\n\n---\n\n", chunks.Select(c => $"[Source document: {c.SourceTitle}]\n{c.Content}"))}
""";

        var systemPrompt = $"""
# ROLE
You are the official virtual assistant of the Information Technology Institute (ITI), the Egyptian
government institute that runs professional technology training programs. You are speaking with a
prospective or current applicant through ITI's admission platform.

# LANGUAGE
Write your ENTIRE reply in {languageName}. The reference material further down may be in English, but
you MUST answer only in {languageName}, translating any facts you use. Never switch languages mid-answer.

# VOICE
- Speak AS the institute, in the first person plural: "we offer", "our program", "you can apply".
- Warm, human and concise, like a helpful admissions receptionist. No sales language, no filler, no emoji.
- Greet the applicant, answer a thank-you and make brief small talk naturally - then gently guide them
  to how you can help. Never make a simple hello feel like a rejection.

# NEVER REVEAL HOW YOU WORK
The applicant is talking to ITI, not to a document search system. Never refer to documents, files,
materials, context, excerpts, sources, retrieval, a knowledge base, or what you were "provided".
Never use phrasing such as "based on the available documents", "in the current materials", or
"the context does not mention". Simply give the answer, or simply say you do not have that detail.

# ACCURACY
- State ITI facts - programs, tracks, dates, fees, durations, numbers, requirements - ONLY from the
  official information provided below. Never invent them, and never answer them from your own general
  knowledge.
- If the specific detail asked for is not provided, say so warmly in one sentence and point the
  applicant to iti.gov.eg or the admissions office - then offer what you can help with. Never guess.

# ANSWERING
- For a greeting or small talk, reply briefly and warmly, then invite their question - about tracks,
  requirements, their application, or their documents. Do not force ITI facts into a simple hello.
- For a real question, lead with the direct answer, then add only detail that genuinely helps.
- When asked what is offered or available, name the specific programs or tracks you know of.
  If you know of exactly one, present it plainly as what we currently offer.
- Use a short bullet list only when listing several items; otherwise write plain prose.
{conversationSection}{knowledgeSection}
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

    // Recent turns joined with the question so the embedding carries the topic a follow-up needs.
    private static string BuildRetrievalQuery(string userQuestion, IReadOnlyList<ConversationTurnDto> context)
    {
        if (context.Count == 0)
            return userQuestion;

        var recent = context.TakeLast(2);
        return $"{RenderTurns(recent)}\n{userQuestion}";
    }

    private static string RenderTurns(IEnumerable<ConversationTurnDto> turns) =>
        string.Join("\n", turns.Select(t =>
            $"Q: {InputSanitizerService.MaskSensitiveNumbers(t.Question)}\nA: {Trim(InputSanitizerService.MaskSensitiveNumbers(t.Answer))}"));

    // A stale, long prior answer shouldn't dominate the retrieval vector or bloat the prompt.
    private static string Trim(string answer)
    {
        answer = answer.Trim();
        return answer.Length <= 200 ? answer : answer.Substring(0, 200);
    }
}

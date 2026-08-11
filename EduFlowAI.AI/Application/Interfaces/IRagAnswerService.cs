using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IRagAnswerService
{
    // context is the recent conversation (oldest first), used to resolve follow-ups like
    // "what about its prerequisites?" - pass an empty list for the first turn.
    // language is "en" or "ar" and decides the language of the answer.
    // searchQuery is the router's self-contained English query for the document lookup. When supplied it
    // is what gets embedded, which keeps small talk in the history out of the search vector and bridges an
    // Arabic question to an English knowledge base. Null falls back to the question joined with context.
    Task<RagAnswerDto> AnswerWithContextAsync(
        string userQuestion,
        IReadOnlyList<ConversationTurnDto> context,
        string language,
        string? searchQuery = null,
        CancellationToken cancellationToken = default);
}

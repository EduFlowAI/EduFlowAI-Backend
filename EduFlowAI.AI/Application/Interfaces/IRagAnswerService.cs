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
    Task<RagAnswerDto> AnswerWithContextAsync(
        string userQuestion,
        IReadOnlyList<ConversationTurnDto> context,
        string language,
        CancellationToken cancellationToken = default);
}

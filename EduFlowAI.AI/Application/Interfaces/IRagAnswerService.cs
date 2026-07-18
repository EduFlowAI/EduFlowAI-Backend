using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IRagAnswerService
{
    // language is "en" or "ar" and decides the language of the answer.
    Task<RagAnswerDto> AnswerWithContextAsync(
        string userQuestion,
        string language,
        CancellationToken cancellationToken = default);
}

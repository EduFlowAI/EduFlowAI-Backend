using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IStatusAnswerService
{
    // userId comes from the caller's token (null/empty for a guest). language is "en" or "ar".
    Task<StatusAnswerDto> AnswerStatusAsync(
        string? userId,
        string language,
        CancellationToken cancellationToken = default);
}

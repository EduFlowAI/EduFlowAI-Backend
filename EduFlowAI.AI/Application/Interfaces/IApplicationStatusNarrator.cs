using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

// Turns an applicant's application status into a warm, natural reply. The status is an authoritative
// fact the model phrases - it never decides, changes or invents it.
public interface IApplicationStatusNarrator
{
    Task<string> NarrateAsync(
        string currentStatus,
        DateTimeOffset lastUpdatedAt,
        string language,
        bool isContinuingConversation = false,
        CancellationToken cancellationToken = default);
}

using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

/// <summary>
/// Answers "which track/program should I choose" questions. Implemented here as a mock; the
/// Agents module owner replaces the registration with the real Track Recommendation service
/// (same contract) once ready — no controller change required.
/// </summary>
public interface IRecommendationsAgent
{
    Task<string> GetResponseAsync(string question, string language, CancellationToken cancellationToken = default);
}

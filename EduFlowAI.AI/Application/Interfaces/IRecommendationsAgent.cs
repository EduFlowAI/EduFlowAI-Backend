using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

// Mocked here until the Agents module provides the real track recommendation service.
public interface IRecommendationsAgent
{
    Task<string> GetResponseAsync(string question, string language, CancellationToken cancellationToken = default);
}

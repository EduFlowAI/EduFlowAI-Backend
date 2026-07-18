using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

// Mocked here until the Applications module provides the real implementation.
public interface IStatusAgent
{
    Task<string> GetResponseAsync(string question, string language, CancellationToken cancellationToken = default);
}

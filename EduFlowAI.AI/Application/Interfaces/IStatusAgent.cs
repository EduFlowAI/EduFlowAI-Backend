using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

/// <summary>
/// Answers "what is my application status" questions. Implemented here as a mock; the Applications
/// module owner replaces the registration with a real implementation (same contract) once ready.
/// </summary>
public interface IStatusAgent
{
    Task<string> GetResponseAsync(string question, string language, CancellationToken cancellationToken = default);
}

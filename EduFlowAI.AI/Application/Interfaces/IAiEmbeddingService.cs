using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IAiEmbeddingService
{
    /// Communicates with the AI provider to generate a 768-dimensional geometric vector for a given text chunk.
   
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
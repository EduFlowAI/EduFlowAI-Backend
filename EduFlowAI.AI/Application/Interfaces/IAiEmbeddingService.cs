using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IAiEmbeddingService
{
    /// Communicates with the AI provider to generate an embedding vector for a text chunk.
    /// The width is configured via Gemini:EmbeddingDimensions and must match the database
    /// vector column.
   
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
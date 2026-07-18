using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

/// <summary>
/// Turns an uploaded document's raw bytes into plain text ready for chunking and embedding.
/// Implementations decide how (AI transcription, local parsing, or plain decoding for text files).
/// </summary>
public interface IDocumentTextExtractor
{
    Task<string> ExtractTextAsync(byte[] content, string fileName, CancellationToken cancellationToken = default);
}

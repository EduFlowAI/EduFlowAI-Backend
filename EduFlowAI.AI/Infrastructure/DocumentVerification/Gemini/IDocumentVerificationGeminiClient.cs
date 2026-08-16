using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Infrastructure.DocumentVerification.Gemini;

// One raw call to Gemini for one document. Retry-once semantics on a malformed/failed
// structured response live in DocumentVerificationService, not here - this client makes a
// single attempt and either returns the raw JSON text or throws.
// The raw JSON and the model that produced it, which is not always the first one tried.
public sealed record DocumentVerificationGeminiResult(string Json, string ModelName);

public interface IDocumentVerificationGeminiClient
{
    Task<DocumentVerificationGeminiResult> GenerateVerificationJsonAsync(
        string documentType,
        IReadOnlyDictionary<string, string> expectedFields,
        byte[] fileContent,
        string mimeType,
        CancellationToken cancellationToken);
}

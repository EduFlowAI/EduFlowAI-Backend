using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

/// <summary>
/// Thin abstraction over Gemini generateContent calls (RAG answers, intent classification and
/// document transcription). Callers supply prompts; the client returns generated text (empty
/// string when the model returns no candidate). Transport/HTTP concerns, model selection and
/// retry policy live entirely in the infrastructure implementation.
/// </summary>
public interface IGeminiChatClient
{
    /// <summary>Free-text generation, using the configured chat temperature.</summary>
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schema-constrained JSON generation at temperature 0. The response is forced to match
    /// <paramref name="responseSchema"/>, so an enum field cannot come back out of vocabulary
    /// and the result is deterministic for the same input.
    /// </summary>
    Task<string> GenerateJsonAsync(string systemPrompt, string userMessage, object responseSchema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a document (e.g. a PDF) as inline data alongside an instruction, at temperature 0.
    /// Transcribes uploads far more faithfully than local text extraction — it preserves reading
    /// order in multi-column layouts, keeps tables intact and does not corrupt ligatures.
    /// </summary>
    Task<string> GenerateFromDocumentAsync(string instruction, byte[] document, string mimeType, CancellationToken cancellationToken = default);
}

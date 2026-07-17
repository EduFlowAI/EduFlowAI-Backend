using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

/// <summary>
/// Thin abstraction over a Gemini text-generation call. Callers supply a system prompt and a
/// user message; the client returns the generated text (empty string when the model returns no
/// candidate). Transport/HTTP concerns live entirely in the infrastructure implementation.
/// </summary>
public interface IGeminiChatClient
{
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
using System.Linq;

namespace EduFlowAI.AI.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    // Single-value config kept for back-compat; the lists below take precedence when set.
    public string ApiKey { get; set; } = string.Empty;

    // One project's free quota runs out fast. Extra keys (from separate projects) are tried in
    // order on quota/auth failures. Empty falls back to the single ApiKey.
    public List<string> ApiKeys { get; set; } = new();

    public string EmbeddingModel { get; set; } = "models/gemini-embedding-001";

    // Must match the vector column width; changing it needs a migration and a full re-sync.
    // Embeddings never fall back to another model - a different model means a different vector
    // space, which silently breaks retrieval.
    public int EmbeddingDimensions { get; set; } = 1536;

    // gemini-2.5-flash returns 404 for newly issued keys.
    public string ChatModel { get; set; } = "gemini-3.1-flash-lite";

    // Tried in order when a model is rate-limited, quota-exhausted or unavailable. Each model has
    // its own free-tier quota bucket, so this is the main quota lever on a single key. Empty falls
    // back to the single ChatModel. Every entry must be document-capable (extraction shares them).
    public List<string> ChatModels { get; set; } = new();

    public double ChatTemperature { get; set; } = 0.2;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    public IReadOnlyList<string> ResolvedKeys =>
        ApiKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() is { Count: > 0 } keys
            ? keys
            : new List<string> { ApiKey };

    public IReadOnlyList<string> ResolvedChatModels =>
        ChatModels.Where(m => !string.IsNullOrWhiteSpace(m)).ToList() is { Count: > 0 } models
            ? models
            : new List<string> { ChatModel };
}

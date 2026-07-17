namespace EduFlowAI.AI.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Embedding model id, including the "models/" prefix (embedContent endpoint).</summary>
    public string EmbeddingModel { get; set; } = "models/gemini-embedding-001";

    /// <summary>
    /// Chat/generation model id, without the "models/" prefix (generateContent endpoint).
    /// Must be a currently-served model for this key: gemini-2.5-flash returns 404 "no longer
    /// available to new users", and gemini-flash-latest can 503 under load. gemini-3.1-flash-lite
    /// is verified 200 for this key and is cheap/fast — a good default for the RAG + intent calls.
    /// </summary>
    public string ChatModel { get; set; } = "gemini-3.1-flash-lite";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
}

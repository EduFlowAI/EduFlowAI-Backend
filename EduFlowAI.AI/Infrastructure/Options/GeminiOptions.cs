namespace EduFlowAI.AI.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Embedding model id, including the "models/" prefix (embedContent endpoint).</summary>
    public string EmbeddingModel { get; set; } = "models/gemini-embedding-001";

    /// <summary>
    /// Output width requested from the embedding model. gemini-embedding-001 is a Matryoshka model
    /// producing 3072 dimensions natively and degrading gracefully when truncated. 1536 retains
    /// far more signal than 768 while staying under pgvector's 2000-dimension limit for ANN
    /// indexes, keeping that option open if the corpus grows. Changing this requires a migration
    /// of the vector column plus a full re-sync.
    /// </summary>
    public int EmbeddingDimensions { get; set; } = 1536;

    /// <summary>
    /// Chat/generation model id, without the "models/" prefix (generateContent endpoint).
    /// Must be a currently-served model for this key: gemini-2.5-flash returns 404 "no longer
    /// available to new users", and gemini-flash-latest can 503 under load. gemini-3.1-flash-lite
    /// is verified working and also accepts PDF input for document transcription.
    /// </summary>
    public string ChatModel { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>
    /// Temperature for free-text answers. Low (not zero) keeps grounded RAG answers faithful to
    /// the retrieved context while still reading naturally. Classification and transcription
    /// always use 0 regardless of this value.
    /// </summary>
    public double ChatTemperature { get; set; } = 0.2;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
}

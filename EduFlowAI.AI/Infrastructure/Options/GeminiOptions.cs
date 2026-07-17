namespace EduFlowAI.AI.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Embedding model id, including the "models/" prefix (embedContent endpoint).</summary>
    public string EmbeddingModel { get; set; } = "models/gemini-embedding-001";

    /// <summary>Chat/generation model id, without the "models/" prefix (generateContent endpoint).</summary>
    public string ChatModel { get; set; } = "gemini-2.5-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
}

namespace EduFlowAI.AI.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = "models/gemini-embedding-001";

    // Must match the vector column width; changing it needs a migration and a full re-sync.
    public int EmbeddingDimensions { get; set; } = 1536;

    // gemini-2.5-flash returns 404 for newly issued keys.
    public string ChatModel { get; set; } = "gemini-3.1-flash-lite";

    public double ChatTemperature { get; set; } = 0.2;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
}

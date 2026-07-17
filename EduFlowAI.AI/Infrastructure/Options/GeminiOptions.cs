namespace EduFlowAI.AI.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "models/gemini-embedding-001";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
}
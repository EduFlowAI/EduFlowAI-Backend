namespace EduFlowAI.AI.Infrastructure.Options;

public sealed class GeminiRecommendationOptions
{
    public const string SectionName = "Gemini:Recommendation";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    // The ranking should be stable while the one-line reason still reads naturally.
    public double Temperature { get; init; } = 0.2;

    public int TimeoutSeconds { get; init; } = 30;
}
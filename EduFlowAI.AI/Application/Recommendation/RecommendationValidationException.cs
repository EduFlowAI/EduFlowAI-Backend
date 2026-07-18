namespace EduFlowAI.AI.Application.Recommendation;

public sealed class RecommendationValidationException(
    IReadOnlyList<string> errors)
    : Exception("The recommendation questionnaire is invalid.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
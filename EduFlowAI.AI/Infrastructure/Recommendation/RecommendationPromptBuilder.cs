using System.Text.Json;
using EduFlowAI.AI.Application.Recommendation.Models;

namespace EduFlowAI.AI.Infrastructure.Recommendation;

internal static class RecommendationPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static string Build(RecommendationModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var input = new
        {
            questionnaire = request.Questionnaire,
            offeredTracks = request.OfferedTracks.Select(track => new
            {
                trackId = track.TrackId,
                name = track.Name,
                description = track.Description,
                prerequisiteTopics = track.PrerequisiteTopics
            })
        };

        return $$"""
            You are an advisory track recommendation assistant for the ITI admission platform.

            Use only the supplied questionnaire and offered track metadata.

            Rules:
            - Return at most three distinct recommendations.
            - Use only the supplied trackId values.
            - Rank results from 1 upward.
            - Give one concise reason for each recommendation.
            - Do not invent tracks, IDs, prerequisites, scores, or percentages.
            - Recommendations are advisory and do not determine eligibility or admission.
            - Return JSON matching the requested schema exactly.

            Input:
            {{JsonSerializer.Serialize(input, JsonOptions)}}
            """;
    }
}
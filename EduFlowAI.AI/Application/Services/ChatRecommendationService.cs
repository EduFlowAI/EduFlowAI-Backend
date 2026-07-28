using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Infrastructure.ExternalServices.Gemini.DTOs;

namespace EduFlowAI.AI.Application.Services;

public sealed class ChatRecommendationService(
    IOfferedTrackReader offeredTrackReader,
    IRecommendationModelClient recommendationModelClient)
    : IChatRecommendationService
{
    private const string AdvisoryNotice =
        "These recommendations are advisory only and do not determine eligibility or admission.";

    public async Task<ChatRecommendationResultDto> RecommendFromMessageAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var offeredTracks =
            await offeredTrackReader.GetActiveOfferedTracksAsync(
                cancellationToken);

        if (offeredTracks.Count == 0)
        {
            return CreateQuestionnaireFallback(
                "No active offered tracks are currently available.");
        }

        try
        {
            var modelResponse =
                await recommendationModelClient.RankFromMessageAsync(
                    new ChatRecommendationModelRequestDto
                    {
                        Message = message,
                        OfferedTracks = offeredTracks
                    },
                    cancellationToken);

            var tracksById = offeredTracks.ToDictionary(
                track => track.TrackId);

            var recommendations = modelResponse.Recommendations
                .Where(item => tracksById.ContainsKey(item.TrackId))
                .GroupBy(item => item.TrackId)
                .Select(group => group.First())
                .OrderBy(item => item.Rank)
                .Take(3)
                .Select((item, index) =>
                {
                    var offeredTrack = tracksById[item.TrackId];

                    return new RecommendedTrackResultDto
                    {
                        TrackId = offeredTrack.TrackId,
                        TrackName = offeredTrack.Name,
                        Rank = index + 1,
                        Reason = item.Reason
                    };
                })
                .ToList();

            if (recommendations.Count == 0)
            {
                return CreateQuestionnaireFallback(
                    "I need more information to recommend a suitable track. Please use the full recommendation questionnaire.");
            }

            return new ChatRecommendationResultDto
            {
                Message = AdvisoryNotice,
                ShouldUseQuestionnaire = true,
                Recommendations = recommendations
            };
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return CreateQuestionnaireFallback(
                "Track recommendations are temporarily unavailable. Please use the recommendation questionnaire.");
        }
        catch (Exception)
        {
            return CreateQuestionnaireFallback(
                "I could not generate a reliable recommendation from this message. Please use the full recommendation questionnaire.");
        }
    }

    private static ChatRecommendationResultDto CreateQuestionnaireFallback(
        string message)
    {
        return new ChatRecommendationResultDto
        {
            Message = message,
            ShouldUseQuestionnaire = true,
            Recommendations = []
        };
    }
}
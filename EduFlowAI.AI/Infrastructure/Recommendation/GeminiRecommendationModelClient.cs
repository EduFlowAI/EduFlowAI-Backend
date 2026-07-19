using System.Net.Http.Json;
using System.Text.Json;
using EduFlowAI.AI.Application.Recommendation.Contracts;
using EduFlowAI.AI.Application.Recommendation.Models;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Infrastructure.Recommendation;

public sealed class GeminiRecommendationModelClient(
    HttpClient httpClient,
    IOptions<GeminiRecommendationOptions> options)
    : IRecommendationModelClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly GeminiRecommendationOptions _options = options.Value;

    public async Task<RecommendationModelResponse> RankAsync(
        RecommendationModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        var endpoint =
            $"v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent";

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);

        message.Headers.Add("x-goog-api-key", _options.ApiKey);

        message.Content = JsonContent.Create(CreatePayload(request), options: JsonOptions);

        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<GeminiResponseEnvelope>(
            JsonOptions,
            cancellationToken);

        var json = envelope?
            .Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .FirstOrDefault()?
            .Text;

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                "Gemini returned an empty recommendation response.");
        }

        return JsonSerializer.Deserialize<RecommendationModelResponse>(
                   json,
                   JsonOptions)
               ?? throw new JsonException(
                   "Gemini returned an invalid recommendation response.");
    }

    private static object CreatePayload(RecommendationModelRequest request)
    {
        return new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = RecommendationPromptBuilder.Build(request)
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        recommendations = new
                        {
                            type = "array",
                            maxItems = 3,
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    trackId = new
                                    {
                                        type = "string"
                                    },
                                    rank = new
                                    {
                                        type = "integer"
                                    },
                                    reason = new
                                    {
                                        type = "string"
                                    }
                                },
                                required = new[]
                                {
                                    "trackId",
                                    "rank",
                                    "reason"
                                }
                            }
                        }
                    },
                    required = new[]
                    {
                        "recommendations"
                    }
                }
            }
        };
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini recommendation API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException(
                "Gemini recommendation model is not configured.");
        }
    }

    private sealed record GeminiResponseEnvelope
    {
        public IReadOnlyList<GeminiCandidate>? Candidates { get; init; }
    }

    private sealed record GeminiCandidate
    {
        public GeminiContent? Content { get; init; }
    }

    private sealed record GeminiContent
    {
        public IReadOnlyList<GeminiPart>? Parts { get; init; }
    }

    private sealed record GeminiPart
    {
        public string? Text { get; init; }
    }
}
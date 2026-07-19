using EduFlowAI.AI.Application.Recommendation;
using EduFlowAI.AI.Application.Recommendation.Contracts;
using EduFlowAI.AI.Infrastructure.Recommendation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI;

public static class AIModule
{
    public static IServiceCollection AddAIModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GeminiRecommendationOptions>()
            .Bind(configuration.GetSection(
                GeminiRecommendationOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "Gemini recommendation API key is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "Gemini recommendation model is required.")
            .Validate(
                options => options.TimeoutSeconds is > 0 and <= 120,
                "Gemini recommendation timeout must be between 1 and 120 seconds.")
            .ValidateOnStart();

        services.AddScoped<
            ITrackRecommendationService,
            TrackRecommendationService>();

        services.AddHttpClient<
                IRecommendationModelClient,
                GeminiRecommendationModelClient>()
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<
                        IOptions<GeminiRecommendationOptions>>()
                    .Value;

                client.BaseAddress =
                    new Uri("https://generativelanguage.googleapis.com/");

                client.Timeout =
                    TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

        // Register the development implementation of IOfferedTrackReader
        // This is a temporary solution for development purposes.
        // l7d ma boda y implement el interfece
        services.AddScoped<
                IOfferedTrackReader,
                DevelopmentOfferedTrackReader>();
        

        return services;
    }
}
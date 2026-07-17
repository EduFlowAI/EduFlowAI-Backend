using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Infrastructure.Caching;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using EduFlowAI.AI.Infrastructure.Options;
using EduFlowAI.AI.Infrastructure.Persistence;
using EduFlowAI.AI.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlowAI.AI;

public static class AIModule
{
    // Parameterless to match the team's Program.cs call: builder.Services.AddAIModule();
    // GeminiOptions is bound from the "Gemini" configuration section via BindConfiguration,
    // which resolves IConfiguration from DI (no need to pass it in explicitly).
    public static IServiceCollection AddAIModule(this IServiceCollection services)
    {
        services.AddTransient<PdfProcessingService>();

        services.AddOptions<GeminiOptions>()
            .BindConfiguration(GeminiOptions.SectionName);

        services.AddHttpClient<IAiEmbeddingService, GeminiEmbeddingService>();

        services.AddScoped<KnowledgeBaseService>();
        services.AddScoped<KnowledgeBaseRetrievalService>();
        services.AddScoped<AiChatService>();

        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();

        services.AddSingleton<ISessionManager, InMemorySessionManager>();

        services.AddHttpClient<IIntentClassifier, IntentClassifierService>();

        services.AddSingleton<EmbeddingCacheService>();

        return services;
    }
}

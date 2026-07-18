using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Infrastructure.Caching;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using EduFlowAI.AI.Infrastructure.Options;
using EduFlowAI.AI.Infrastructure.Persistence;
using EduFlowAI.AI.Infrastructure.Processing;
using EduFlowAI.AI.Infrastructure.Services;
using EduFlowAI.AI.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduFlowAI.AI;

public static class AIModule
{
    public static IServiceCollection AddAIModule(this IServiceCollection services)
    {
        services.AddTransient<PdfProcessingService>();

        services.AddOptions<GeminiOptions>()
            .BindConfiguration(GeminiOptions.SectionName);

        services.AddOptions<FileStorageOptions>()
            .BindConfiguration(FileStorageOptions.SectionName);

        services.AddOptions<ChatSessionOptions>()
            .BindConfiguration(ChatSessionOptions.SectionName);

        services.AddOptions<IntentClassificationOptions>()
            .BindConfiguration(IntentClassificationOptions.SectionName);

        services.AddOptions<IngestionOptions>()
            .BindConfiguration(IngestionOptions.SectionName);

        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();

        // Injected so session expiry can be tested with a fake clock.
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>();
        services.AddHttpClient<IGeminiChatClient, GeminiChatClient>();

        services.AddScoped<IKnowledgeIndexingService, KnowledgeBaseService>();
        services.AddScoped<IKnowledgeRetrievalService, KnowledgeBaseRetrievalService>();
        services.AddScoped<IRagAnswerService, AiChatService>();
        services.AddScoped<IIntentRouter, IntentClassifierService>();

        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();

        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddSingleton<KnowledgeBaseSyncState>();

        services.AddSingleton<EmbeddingCacheService>();

        return services;
    }
}

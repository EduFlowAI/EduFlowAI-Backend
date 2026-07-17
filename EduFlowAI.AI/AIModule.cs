using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Infrastructure.Caching;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using EduFlowAI.AI.Infrastructure.Options;
using EduFlowAI.AI.Infrastructure.Persistence;
using EduFlowAI.AI.Infrastructure.Services;
using EduFlowAI.AI.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.AddOptions<FileStorageOptions>()
            .BindConfiguration(FileStorageOptions.SectionName);

        services.AddOptions<ChatSessionOptions>()
            .BindConfiguration(ChatSessionOptions.SectionName);

        // Clock abstraction so session expiry is testable with a fake time provider.
        services.TryAddSingleton(TimeProvider.System);

        // Protected local file storage for uploaded PDFs (swappable for cloud storage later).
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        services.AddHttpClient<IAiEmbeddingService, GeminiEmbeddingService>();

        // Single Gemini generateContent client shared by the RAG service and the intent classifier.
        services.AddHttpClient<IGeminiChatClient, GeminiChatClient>();

        services.AddScoped<KnowledgeBaseService>();
        services.AddScoped<KnowledgeBaseRetrievalService>();
        services.AddScoped<AiChatService>();

        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();

        services.AddSingleton<ISessionManager, InMemorySessionManager>();

        // No longer a typed HttpClient — it depends on IGeminiChatClient now.
        services.AddScoped<IIntentClassifier, IntentClassifierService>();

        services.AddSingleton<EmbeddingCacheService>();

        return services;
    }
}

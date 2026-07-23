using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;

namespace EduFlowAI.AI.Tests;

public class AiChatServiceTests
{
    private static readonly IReadOnlyList<ConversationTurnDto> NoHistory = new List<ConversationTurnDto>();

    private static readonly IReadOnlyList<ConversationTurnDto> DotNetHistory = new List<ConversationTurnDto>
    {
        new() { Question = "What tracks suit backend developers?", Answer = "The .NET Enterprise track is a strong option." }
    };

    [Fact]
    public async Task First_turn_retrieves_on_the_raw_question()
    {
        var (service, retrieval, _) = Build();

        await service.AnswerWithContextAsync("What tracks are available?", NoHistory, "en");

        Assert.Equal("What tracks are available?", retrieval.LastQuery);
    }

    [Fact]
    public async Task Follow_up_retrieves_on_history_joined_with_the_question()
    {
        var (service, retrieval, _) = Build();

        await service.AnswerWithContextAsync("What about its prerequisites?", DotNetHistory, "en");

        // The topic (.NET) comes from the prior turn; the question is still included.
        Assert.Contains(".NET Enterprise", retrieval.LastQuery);
        Assert.Contains("What about its prerequisites?", retrieval.LastQuery);
    }

    [Fact]
    public async Task Follow_up_puts_the_prior_dialogue_in_the_answer_prompt()
    {
        var (service, _, chat) = Build();

        await service.AnswerWithContextAsync("What about its prerequisites?", DotNetHistory, "en");

        Assert.Contains("CONVERSATION SO FAR", chat.LastSystemPrompt);
        Assert.Contains("What tracks suit backend developers?", chat.LastSystemPrompt);
        Assert.Contains(".NET Enterprise", chat.LastSystemPrompt);
    }

    [Fact]
    public async Task First_turn_adds_no_conversation_section()
    {
        var (service, _, chat) = Build();

        await service.AnswerWithContextAsync("What tracks are available?", NoHistory, "en");

        Assert.DoesNotContain("CONVERSATION SO FAR", chat.LastSystemPrompt);
    }

    [Fact]
    public async Task No_chunks_returns_the_fallback_without_calling_the_model()
    {
        var (service, retrieval, chat) = Build();
        retrieval.Chunks = new List<RetrievedChunkDto>();   // nothing indexed matches

        var en = await service.AnswerWithContextAsync("anything", NoHistory, "en");
        var ar = await service.AnswerWithContextAsync("أي شيء", NoHistory, "ar");

        Assert.Contains("don't have that", en.Answer);
        Assert.Contains("لا تتوفر", ar.Answer);
        Assert.Null(chat.LastSystemPrompt);   // never reached the model
    }

    private static (AiChatService, FakeRetrieval, StubChat) Build()
    {
        var retrieval = new FakeRetrieval();
        var chat = new StubChat();
        return (new AiChatService(retrieval, chat), retrieval, chat);
    }

    private sealed class FakeRetrieval : IKnowledgeRetrievalService
    {
        public string? LastQuery { get; private set; }

        public List<RetrievedChunkDto> Chunks { get; set; } = new()
        {
            new() { Content = "The .NET Enterprise track runs for nine months.", SourceTitle = "dotnet.md" }
        };

        public Task<List<RetrievedChunkDto>> RetrieveContextAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Chunks);
        }
    }

    private sealed class StubChat : IGeminiChatClient
    {
        public string? LastSystemPrompt { get; private set; }

        public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        {
            LastSystemPrompt = systemPrompt;
            return Task.FromResult("an answer");
        }

        public Task<string> GenerateJsonAsync(string systemPrompt, string userMessage, object responseSchema, CancellationToken ct = default)
            => Task.FromResult(string.Empty);

        public Task<string> GenerateFromDocumentAsync(string instruction, byte[] document, string mimeType, CancellationToken ct = default)
            => Task.FromResult(string.Empty);
    }
}

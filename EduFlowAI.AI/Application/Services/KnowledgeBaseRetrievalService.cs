using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Application.Services;

public class KnowledgeBaseRetrievalService : IKnowledgeRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IKnowledgeRepository _repository;

    public KnowledgeBaseRetrievalService(IEmbeddingService embeddingService, IKnowledgeRepository repository)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<RetrievedChunkDto>> RetrieveContextAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<RetrievedChunkDto>();

        var queryVectorArray = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        var queryVector = new Pgvector.Vector(queryVectorArray);

        return await _repository.SearchChunksByEmbeddingAsync(queryVector, limit, cancellationToken);
    }
}

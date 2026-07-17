using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Application.Services;

public class KnowledgeBaseRetrievalService
{
    private readonly IAiEmbeddingService _embeddingService;
    private readonly IKnowledgeRepository _repository;

    public KnowledgeBaseRetrievalService(IAiEmbeddingService embeddingService, IKnowledgeRepository repository)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// Converts a user search query into embeddings, and queries PostgreSQL using Cosine Distance
    /// to return the top N most semantically relevant text chunks.

    public async Task<List<string>> RetrieveContextAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<string>();

        //  Generate coordinates for the query text using Gemini
        float[] queryVectorArray = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        var queryVector = new Pgvector.Vector(queryVectorArray);

        //  Execute pgvector cosine distance sorting via the repository
        var matchedChunks = await _repository.SearchChunksByEmbeddingAsync(queryVector, limit, cancellationToken);

        return matchedChunks;
    }
}
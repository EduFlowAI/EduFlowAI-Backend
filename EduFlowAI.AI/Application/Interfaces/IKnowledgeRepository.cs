using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Domain.Entities;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IKnowledgeRepository
{
    Task<KnowledgeBaseDocument?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<KnowledgeBaseDocument>> GetAllDocumentsAsync(CancellationToken cancellationToken = default);
    Task AddDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default);
    Task UpdateDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default);

    Task AddChunksAsync(List<KnowledgeBaseChunk> chunks, CancellationToken cancellationToken = default);
    Task<int> GetChunkCountAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<List<string>> SearchChunksByEmbeddingAsync(
        Pgvector.Vector embedding,
        int limit = 5,
        CancellationToken cancellationToken = default);
}
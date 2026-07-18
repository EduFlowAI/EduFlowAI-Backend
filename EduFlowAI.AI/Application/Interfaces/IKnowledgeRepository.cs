using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Domain.Entities;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IKnowledgeRepository
{
    Task<KnowledgeBaseDocument?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<KnowledgeBaseDocument>> GetAllDocumentsAsync(CancellationToken cancellationToken = default);
    Task AddDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default);
    Task UpdateDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default);

    Task DeleteDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default);

    Task AddChunksAsync(List<KnowledgeBaseChunk> chunks, CancellationToken cancellationToken = default);
    Task<int> GetChunkCountAsync(Guid documentId, CancellationToken cancellationToken = default);

    // Used by a full re-sync.
    Task DeleteAllChunksAsync(CancellationToken cancellationToken = default);
    // Nearest chunks by cosine distance, each with its source document.
    Task<List<RetrievedChunkDto>> SearchChunksByEmbeddingAsync(
        Pgvector.Vector embedding,
        int limit = 5,
        CancellationToken cancellationToken = default);
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Domain.Entities;

namespace EduFlowAI.AI.Infrastructure.Persistence;

public class KnowledgeRepository : IKnowledgeRepository
{
    private readonly DbContext _context;

    public KnowledgeRepository(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<KnowledgeBaseDocument?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<KnowledgeBaseDocument>()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<List<KnowledgeBaseDocument>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<KnowledgeBaseDocument>().ToListAsync(cancellationToken);
    }

    public async Task AddDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default)
    {
        await _context.Set<KnowledgeBaseDocument>().AddAsync(document, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default)
    {
        _context.Set<KnowledgeBaseDocument>().Update(document);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default)
    {
        // Chunks are removed by the FK cascade configured on KnowledgeBaseChunk.
        _context.Set<KnowledgeBaseDocument>().Remove(document);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllChunksAsync(CancellationToken cancellationToken = default)
    {
        await _context.Set<KnowledgeBaseChunk>().ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddChunksAsync(List<KnowledgeBaseChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _context.Set<KnowledgeBaseChunk>().AddRangeAsync(chunks, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetChunkCountAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<KnowledgeBaseChunk>()
            .CountAsync(c => c.KnowledgeBaseDocumentId == documentId, cancellationToken);
    }

    public async Task<List<RetrievedChunkDto>> SearchChunksByEmbeddingAsync(
        Pgvector.Vector embedding,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        // Join to the parent document so each chunk carries its source title.
        return await _context.Set<KnowledgeBaseChunk>()
            .OrderBy(c => c.Embedding.CosineDistance(embedding))
            .Take(limit)
            .Select(c => new RetrievedChunkDto
            {
                Content = c.Content,
                DocumentId = c.KnowledgeBaseDocumentId,
                SourceTitle = c.Document.FileName
            })
            .ToListAsync(cancellationToken);
    }
}
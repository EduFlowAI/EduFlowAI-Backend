using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.AI.Domain.Enums;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging;

namespace EduFlowAI.AI.Application.Services;

public class KnowledgeBaseService
{
    private readonly PdfProcessingService _pdfProcessor;
    private readonly IAiEmbeddingService _embeddingService;
    private readonly IKnowledgeRepository _repository;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(
        PdfProcessingService pdfProcessor,
        IAiEmbeddingService embeddingService,
        IKnowledgeRepository repository,
        IFileStorageService fileStorage,
        ILogger<KnowledgeBaseService> logger)
    {
        _pdfProcessor = pdfProcessor ?? throw new ArgumentNullException(nameof(pdfProcessor));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Extracts, chunks, embeds and stores a previously-uploaded document. The physical PDF is
    /// read from protected storage using the document's StorageKey, so this can be re-run during
    /// a knowledge-base re-sync without the original HTTP upload stream.
    /// </summary>
    public async Task IngestDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetDocumentByIdAsync(documentId, cancellationToken);

        if (document == null)
            throw new KeyNotFoundException($"Knowledge Base Document with ID {documentId} was not found.");

        try
        {
            await IndexDocumentAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(document, ex, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Rebuilds the whole knowledge base: deletes every chunk, then re-indexes each stored
    /// document from its PDF. A document whose file is missing or unreadable is marked Failed and
    /// the rest continue (team plan Part 8).
    /// </summary>
    public async Task<KnowledgeBaseSyncResultDto> ResyncAllAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _repository.GetAllDocumentsAsync(cancellationToken);

        // Wipe first so a failed document cannot leave stale vectors behind.
        await _repository.DeleteAllChunksAsync(cancellationToken);

        var result = new KnowledgeBaseSyncResultDto { TotalDocuments = documents.Count };

        foreach (var document in documents)
        {
            try
            {
                await IndexDocumentAsync(document, cancellationToken);
                result.Indexed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Re-sync failed for document {DocumentId} ({FileName})", document.Id, document.FileName);
                await MarkFailedAsync(document, ex, cancellationToken);
                result.Failed++;
            }
        }

        _logger.LogInformation(
            "Knowledge base re-sync finished: {Indexed} indexed, {Failed} failed of {Total}.",
            result.Indexed, result.Failed, result.TotalDocuments);

        return result;
    }

    /// <summary>Deletes a document, its chunks (FK cascade), and its stored file. False if not found.</summary>
    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document == null)
            return false;

        var storageKey = document.StorageKey;

        await _repository.DeleteDocumentAsync(document, cancellationToken);

        // Delete the file only after the database change succeeded, so a DB failure can't orphan the row.
        if (!string.IsNullOrWhiteSpace(storageKey))
            await _fileStorage.DeleteAsync(storageKey, cancellationToken);

        return true;
    }

    // Shared indexing path used by both a first upload and a re-sync.
    private async Task IndexDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken)
    {
        document.Status = KnowledgeBaseStatus.Indexing;
        await _repository.UpdateDocumentAsync(document, cancellationToken);

        List<string> rawTextChunks;
        await using (var pdfStream = await _fileStorage.OpenReadAsync(document.StorageKey, cancellationToken))
        {
            rawTextChunks = _pdfProcessor.ExtractChunksFromPdf(pdfStream);
        }

        if (rawTextChunks.Count == 0)
            throw new InvalidOperationException("The uploaded PDF did not yield any readable text lines.");

        var embeddedChunks = new List<KnowledgeBaseChunk>();
        foreach (var textContent in rawTextChunks)
        {
            var coordinateArray = await _embeddingService.GenerateEmbeddingAsync(textContent, cancellationToken);

            embeddedChunks.Add(new KnowledgeBaseChunk
            {
                KnowledgeBaseDocumentId = document.Id,
                Content = textContent,
                Embedding = new Pgvector.Vector(coordinateArray)
            });
        }

        await _repository.AddChunksAsync(embeddedChunks, cancellationToken);

        document.Status = KnowledgeBaseStatus.Indexed;
        document.ErrorMessage = null;
        await _repository.UpdateDocumentAsync(document, cancellationToken);
    }

    private async Task MarkFailedAsync(KnowledgeBaseDocument document, Exception ex, CancellationToken cancellationToken)
    {
        document.Status = KnowledgeBaseStatus.Failed;
        document.ErrorMessage = ex.Message;
        await _repository.UpdateDocumentAsync(document, cancellationToken);
    }
}

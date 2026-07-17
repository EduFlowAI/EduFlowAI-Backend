using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.AI.Domain.Enums;
using EduFlowAI.AI.Infrastructure.ExternalServices;

namespace EduFlowAI.AI.Application.Services;

public class KnowledgeBaseService
{
    private readonly PdfProcessingService _pdfProcessor;
    private readonly IAiEmbeddingService _embeddingService;
    private readonly IKnowledgeRepository _repository;
    private readonly IFileStorageService _fileStorage;

    public KnowledgeBaseService(
        PdfProcessingService pdfProcessor,
        IAiEmbeddingService embeddingService,
        IKnowledgeRepository repository,
        IFileStorageService fileStorage)
    {
        _pdfProcessor = pdfProcessor ?? throw new ArgumentNullException(nameof(pdfProcessor));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
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
            await _repository.UpdateDocumentAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            // Capture the failure reason on the document so the status endpoint can surface it.
            document.Status = KnowledgeBaseStatus.Failed;
            document.ErrorMessage = ex.Message;
            await _repository.UpdateDocumentAsync(document, cancellationToken);

            throw;
        }
    }
}

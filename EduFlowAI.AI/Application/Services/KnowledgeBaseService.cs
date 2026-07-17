using System;
using System.Collections.Generic;
using System.IO;
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

    public KnowledgeBaseService(
        PdfProcessingService pdfProcessor,
        IAiEmbeddingService embeddingService,
        IKnowledgeRepository repository)
    {
        _pdfProcessor = pdfProcessor ?? throw new ArgumentNullException(nameof(pdfProcessor));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task IngestDocumentAsync(Guid documentId, Stream pdfStream, CancellationToken cancellationToken = default)
    {
        
        var document = await _repository.GetDocumentByIdAsync(documentId, cancellationToken);

        if (document == null)
            throw new KeyNotFoundException($"Knowledge Base Document with ID {documentId} was not found.");

        try
        {
            // Flip status to Indexing to notify the UI
            document.Status = KnowledgeBaseStatus.Indexing;
            await _repository.UpdateDocumentAsync(document, cancellationToken);

            // Extract text and slice into clean overlapping chunks
            var rawTextChunks = _pdfProcessor.ExtractChunksFromPdf(pdfStream);

            if (rawTextChunks.Count == 0)
            {
                throw new InvalidOperationException("The uploaded PDF did not yield any readable text lines.");
            }

            var embeddedChunks = new List<KnowledgeBaseChunk>();

            // Process each text block sequentially through Google's vector space
            foreach (var textContent in rawTextChunks)
            {
                float[] coordinateArray = await _embeddingService.GenerateEmbeddingAsync(textContent, cancellationToken);
                var vectorWrapper = new Pgvector.Vector(coordinateArray);

                var chunkEntity = new KnowledgeBaseChunk
                {
                    KnowledgeBaseDocumentId = document.Id,
                    Content = textContent,
                    Embedding = vectorWrapper
                };

                embeddedChunks.Add(chunkEntity);
            }

            // Bulk insert all vectorized text chunks into PostgreSQL
            await _repository.AddChunksAsync(embeddedChunks, cancellationToken);

            document.Status = KnowledgeBaseStatus.Indexed;
            await _repository.UpdateDocumentAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-safe: capture system or network failure logs directly inside the document record
            document.Status = KnowledgeBaseStatus.Failed;
            document.ErrorMessage = ex.Message;
            await _repository.UpdateDocumentAsync(document, cancellationToken);

            throw;
        }
    }
}
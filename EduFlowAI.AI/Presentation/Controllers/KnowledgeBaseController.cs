using System;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.AI.Domain.Enums;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.AI.Presentation.Controllers;

[ApiController]
[Route("api/ai/knowledge-base")]
public class KnowledgeBaseController : ControllerBase
{
    private const long MaxUploadBytes = 20L * 1024 * 1024; // 20 MB

    private readonly KnowledgeBaseService _knowledgeBaseService;
    private readonly IKnowledgeRepository _repository;
    private readonly IFileStorageService _fileStorage;

    public KnowledgeBaseController(
        KnowledgeBaseService knowledgeBaseService,
        IKnowledgeRepository repository,
        IFileStorageService fileStorage)
    {
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
    }

    /// <summary>
    /// Uploads a PDF, stores it in protected storage, then extracts text and generates vector
    /// embeddings via Gemini. SuperAdmin-only once the Identity module is wired (Task 11).
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadDocument(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file was uploaded or the file is completely empty.");

        if (file.Length > MaxUploadBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "The uploaded file exceeds the 20 MB limit.");

        // Content-based validation: a real PDF starts with "%PDF". This cannot be spoofed by
        // simply renaming a non-PDF to .pdf, and it prevents iText from throwing on junk input.
        await using (var probeStream = file.OpenReadStream())
        {
            if (!await PdfFileValidation.HasPdfHeaderAsync(probeStream, cancellationToken))
                return BadRequest("Invalid format. Only standard PDF (.pdf) documents are supported.");
        }

        var documentId = Guid.NewGuid();
        var storageKey = $"knowledge-base/{documentId}.pdf";

        await using (var uploadStream = file.OpenReadStream())
        {
            await _fileStorage.SaveAsync(uploadStream, storageKey, cancellationToken);
        }

        var documentRecord = new KnowledgeBaseDocument
        {
            Id = documentId,
            FileName = file.FileName,
            StorageKey = storageKey,
            // TODO(Task 11 / Identity): use the authenticated SuperAdmin's id from JWT claims.
            UploadedByUserId = "system-seed",
            Status = KnowledgeBaseStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddDocumentAsync(documentRecord, cancellationToken);

        // Coordinates chunking, Gemini vectorization, and chunk storage (reads the stored file).
        await _knowledgeBaseService.IngestDocumentAsync(documentId, cancellationToken);

        return Ok(new
        {
            DocumentId = documentId,
            Message = "Document uploaded and vectorized into PostgreSQL successfully."
        });
    }

    /// <summary>
    /// Diagnostic endpoint — reports a document's current status directly from the database,
    /// including its error message (if indexing failed) and how many chunks were saved.
    /// </summary>
    [HttpGet("{documentId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentStatus(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _repository.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document == null)
            return NotFound($"No document found with ID {documentId}.");

        var chunkCount = await _repository.GetChunkCountAsync(documentId, cancellationToken);

        return Ok(new
        {
            documentId = document.Id,
            fileName = document.FileName,
            status = document.Status.ToString(),
            errorMessage = document.ErrorMessage,
            chunkCount,
            createdAt = document.CreatedAt
        });
    }
}

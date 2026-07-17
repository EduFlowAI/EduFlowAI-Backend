using System;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.AI.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.AI.Presentation.Controllers;

[ApiController]
[Route("api/ai/knowledge-base")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly KnowledgeBaseService _knowledgeBaseService;
    private readonly DbContext _context;

    public KnowledgeBaseController(KnowledgeBaseService knowledgeBaseService, DbContext context)
    {
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Uploads a PDF training manual or document, extracts its text, and generates vector embeddings via Gemini.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(IFormFile file, CancellationToken cancellationToken)
    {
        // 1. Validation guardrails
        if (file == null || file.Length == 0)
            return BadRequest("No file was uploaded or the file is completely empty.");

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid format. Only standard PDF (.pdf) documents are supported.");
        }

        // 2. Initialize the document record tracking entity in a pending state
        var documentRecord = new KnowledgeBaseDocument
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            // TODO(Task 11 / Identity): replace with the authenticated SuperAdmin's id from JWT claims.
            // The team's KnowledgeBaseDocument.UploadedByUserId is a non-nullable string, so a
            // placeholder is required until the Identity module is wired into these endpoints.
            UploadedByUserId = "system-seed",
            // StorageKey is populated properly in Task 5 (file storage). Empty for now so the
            // NOT NULL column is satisfied; the raw PDF is not yet persisted to disk.
            StorageKey = string.Empty,
            Status = KnowledgeBaseStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _context.Set<KnowledgeBaseDocument>().AddAsync(documentRecord, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // 3. Open a stream directly from the HTTP request memory pool and execute the RAG pipeline
        using var fileStream = file.OpenReadStream();

        // This coordinates chunking, Gemini API transmission, vectorization, and DB storage
        await _knowledgeBaseService.IngestDocumentAsync(documentRecord.Id, fileStream, cancellationToken);

        return Ok(new
        {
            DocumentId = documentRecord.Id,
            Message = "Document uploaded and vectorized into PostgreSQL successfully."
        });
    }

    /// <summary>
    /// Diagnostic endpoint — reads a document's current status directly from the database,
    /// including its error message if indexing failed and how many chunks were saved.
    /// </summary>
    [HttpGet("{documentId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentStatus(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _context.Set<KnowledgeBaseDocument>()
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
            return NotFound($"No document found with ID {documentId}.");

        return Ok(new
        {
            documentId = document.Id,
            fileName = document.FileName,
            status = document.Status.ToString(),
            errorMessage = document.ErrorMessage,
            chunkCount = document.Chunks.Count,
            createdAt = document.CreatedAt
        });
    }
}
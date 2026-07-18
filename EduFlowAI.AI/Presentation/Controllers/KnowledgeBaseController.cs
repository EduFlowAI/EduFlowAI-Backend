using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.AI.Domain.Enums;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using EduFlowAI.AI.Infrastructure.Processing;
using EduFlowAI.AI.Infrastructure.Services;
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
    private readonly KnowledgeBaseSyncState _syncState;

    public KnowledgeBaseController(
        KnowledgeBaseService knowledgeBaseService,
        IKnowledgeRepository repository,
        IFileStorageService fileStorage,
        KnowledgeBaseSyncState syncState)
    {
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        _syncState = syncState ?? throw new ArgumentNullException(nameof(syncState));
    }

    // Accepts a PDF or a .txt/.md file. To be restricted to SuperAdmin once auth is wired.
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

        var isPlainText = DocumentTextExtractor.IsPlainText(file.FileName);

        if (!isPlainText)
        {
            await using var probeStream = file.OpenReadStream();
            if (!await PdfFileValidation.HasPdfHeaderAsync(probeStream, cancellationToken))
                return BadRequest("Unsupported file. Upload a PDF, or a .txt / .md text file.");
        }

        var documentId = Guid.NewGuid();
        var extension = isPlainText ? Path.GetExtension(file.FileName).ToLowerInvariant() : ".pdf";
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".txt";
        var storageKey = $"knowledge-base/{documentId}{extension}";

        await using (var uploadStream = file.OpenReadStream())
        {
            await _fileStorage.SaveAsync(uploadStream, storageKey, cancellationToken);
        }

        var documentRecord = new KnowledgeBaseDocument
        {
            Id = documentId,
            FileName = file.FileName,
            StorageKey = storageKey,
            // TODO: replace with the authenticated user id once auth is wired.
            UploadedByUserId = "system-seed",
            Status = KnowledgeBaseStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddDocumentAsync(documentRecord, cancellationToken);

        await _knowledgeBaseService.IngestDocumentAsync(documentId, cancellationToken);

        return Ok(new
        {
            DocumentId = documentId,
            Message = "Document uploaded and vectorized into PostgreSQL successfully."
        });
    }

    // Reads the document row directly, so a slow or failed upload can be told apart.
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

    // Stored like any upload so re-sync rebuilds it the same way.
    [HttpPost("text")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddText([FromBody] AddTextRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Content cannot be empty.");

        var title = string.IsNullOrWhiteSpace(request.Title) ? "Pasted note" : request.Title.Trim();

        var documentId = Guid.NewGuid();
        var storageKey = $"knowledge-base/{documentId}.txt";

        using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes(request.Content)))
        {
            await _fileStorage.SaveAsync(buffer, storageKey, cancellationToken);
        }

        var documentRecord = new KnowledgeBaseDocument
        {
            Id = documentId,
            // The .txt suffix tells the extractor to decode rather than transcribe.
            FileName = title.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? title : $"{title}.txt",
            StorageKey = storageKey,
            // TODO: replace with the authenticated user id once auth is wired.
            UploadedByUserId = "system-seed",
            Status = KnowledgeBaseStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddDocumentAsync(documentRecord, cancellationToken);
        await _knowledgeBaseService.IngestDocumentAsync(documentId, cancellationToken);

        return Ok(new
        {
            DocumentId = documentId,
            Message = "Text added to the knowledge base."
        });
    }

    [HttpGet("documents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocuments(CancellationToken cancellationToken)
    {
        var documents = await _repository.GetAllDocumentsAsync(cancellationToken);

        return Ok(documents.Select(d => new
        {
            documentId = d.Id,
            fileName = d.FileName,
            status = d.Status.ToString(),
            errorMessage = d.ErrorMessage,
            createdAt = d.CreatedAt
        }));
    }

    // Removes the row, its chunks (cascade) and the stored file.
    [HttpDelete("{documentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken cancellationToken)
    {
        // Would race the rebuild currently writing chunks.
        if (_syncState.IsSyncing)
            return Conflict("A knowledge-base sync is in progress. Try again once it completes.");

        var deleted = await _knowledgeBaseService.DeleteDocumentAsync(documentId, cancellationToken);
        if (!deleted)
            return NotFound($"No document found with ID {documentId}.");

        return NoContent();
    }

    // Deletes and regenerates every embedding from the stored files.
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        if (!_syncState.TryBeginSync())
            return Conflict("A knowledge-base sync is already in progress.");

        try
        {
            var result = await _knowledgeBaseService.ResyncAllAsync(cancellationToken);
            return Ok(new
            {
                totalDocuments = result.TotalDocuments,
                indexed = result.Indexed,
                failed = result.Failed
            });
        }
        finally
        {
            _syncState.EndSync();
        }
    }
}

public class AddTextRequest
{
    public string? Title { get; set; }

    public string Content { get; set; } = string.Empty;
}

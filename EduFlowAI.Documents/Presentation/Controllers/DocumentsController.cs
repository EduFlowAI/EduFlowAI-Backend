using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Presentation.Requests;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.Documents.Presentation.Controllers;

[ApiController]
[Authorize]
public sealed class DocumentsController(IApplicantDocumentService documentService) : ControllerBase
{
    [HttpGet("api/applications/{applicationId:guid}/documents")]
    public async Task<IActionResult> List(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (applicationId == Guid.Empty)
            return BadRequest("Application ID is required.");

        var result = await documentService.ListAsync(applicationId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("api/applications/{applicationId:guid}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        Guid applicationId,
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (applicationId == Guid.Empty)
            return BadRequest("Application ID is required.");
        if (request.File is null || request.File.Length == 0)
            return BadRequest("File cannot be null or empty.");

        var result = await documentService.UploadAsync(
            applicationId,
            request.DocumentType,
            request.File,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("api/applications/{applicationId:guid}/documents/submit")]
    public async Task<IActionResult> Submit(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (applicationId == Guid.Empty)
            return BadRequest("Application ID is required.");

        var result = await documentService.SubmitAsync(applicationId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("api/documents/{documentId:guid}/file")]
    public async Task<IActionResult> Download(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty)
            return BadRequest("Document ID is required.");

        var result = await documentService.OpenFileAsync(documentId, cancellationToken);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result);

        return File(
            result.Data.Stream,
            result.Data.ContentType,
            result.Data.DownloadFileName,
            enableRangeProcessing: true);
    }

    private ObjectResult ToActionResult<T>(Result<T> result) =>
        StatusCode(result.StatusCode, result);
}

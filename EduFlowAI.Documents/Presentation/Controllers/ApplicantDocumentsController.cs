using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Presentation.Requests;

namespace EduFlowAI.Documents.Presentation.Controllers;

[ApiController]
[Authorize] // Enforces that the user must be authenticated
[Route("api/applications/{applicationId:guid}/documents")]
public sealed class ApplicantDocumentsController : ControllerBase
{
    private readonly IApplicantDocumentService _documentService;

    public ApplicantDocumentsController(IApplicantDocumentService documentService)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(
        [FromRoute] Guid applicationId,
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Map the HTTP request to your application DTO
        var dto = new UploadDocumentDto(
            applicationId,
            request.DocumentType,
            request.File);

        // 2. Execute the business logic
        var result = await _documentService.UploadDocumentAsync(dto, cancellationToken);

        // 3. Translate the custom Result<T> into a standard HTTP response
        if (result.IsSuccess)
        {
            // Returns the Document ID so the frontend has a reference to what was just created
            return StatusCode(result.StatusCode, new
            {
                documentId = result.Data,
                message = "Document uploaded successfully."
            });
        }

        // Returns the exact failure code (400, 403, 404, 500) and the error message
        return StatusCode(result.StatusCode, new
        {
            error = result.Message
        });
    }


    [HttpGet]
    public async Task<IActionResult> GetDocuments(
    [FromRoute] Guid applicationId,
    CancellationToken cancellationToken)
    {
        var result = await _documentService.GetDocumentsByApplicationIdAsync(
            applicationId,
            cancellationToken);

        if (result.IsSuccess)
        {
            return StatusCode(result.StatusCode, new
            {
                documents = result.Data
            });
        }

        return StatusCode(result.StatusCode, new
        {
            error = result.Message
        });
    }
}
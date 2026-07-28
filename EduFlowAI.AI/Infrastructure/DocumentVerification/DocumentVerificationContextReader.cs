using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.AI.Application.DocumentVerification;
using EduFlowAI.Identity.Application.DTOs;
using EduFlowAI.Identity.Application.Interfaces;

namespace EduFlowAI.AI.Infrastructure.DocumentVerification;

// Resolves the applicant fields Gemini should compare a document against.
// The field mapping per document type is a business decision confirmed with
// the Admission/Identity owners - see SEIF_IMPLEMENTATION_GUIDE.md section 1.
public sealed class DocumentVerificationContextReader : IDocumentVerificationContextReader
{
    private readonly IApplicationAcessReader _applicationAccessReader;
    private readonly IProfileService _profileService;

    public DocumentVerificationContextReader(
        IApplicationAcessReader applicationAccessReader,
        IProfileService profileService)
    {
        _applicationAccessReader = applicationAccessReader;
        _profileService = profileService;
    }

    public async Task<DocumentVerificationContext> GetAsync(
        Guid applicationId,
        string documentType,
        CancellationToken cancellationToken)
    {
        string userId;
        try
        {
            userId = await _applicationAccessReader.GetApplicantUserIdAsync(applicationId, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new DocumentVerificationFinalException(
                "DOCUMENT_CONTEXT_NOT_FOUND",
                "The application for this verification request could not be found.",
                attemptCount: 1,
                innerException: ex);
        }

        var profileResult = await _profileService.GetAsync(userId, cancellationToken);
        if (!profileResult.IsSuccess || profileResult.Value is null)
        {
            throw new DocumentVerificationFinalException(
                "DOCUMENT_CONTEXT_NOT_FOUND",
                "The applicant profile for this verification request could not be found.",
                attemptCount: 1);
        }

        var expectedFields = BuildExpectedFields(documentType, profileResult.Value);
        if (expectedFields.Count == 0)
        {
            throw new DocumentVerificationFinalException(
                "DOCUMENT_CONTEXT_NOT_FOUND",
                $"Document type '{documentType}' is not supported for verification.",
                attemptCount: 1);
        }

        return new DocumentVerificationContext(applicationId, documentType, expectedFields);
    }

    private static IReadOnlyDictionary<string, string> BuildExpectedFields(
        string documentType,
        ProfileResponse profile) => documentType switch
        {
            "NationalId" => Map(
                ("FullNameAr", profile.FullNameAr),
                ("NationalId", profile.NationalId),
                ("DateOfBirth", profile.DateOfBirth?.ToString("yyyy-MM-dd"))),

            "BirthCertificate" => Map(
                ("FullNameAr", profile.FullNameAr),
                ("DateOfBirth", profile.DateOfBirth?.ToString("yyyy-MM-dd"))),

            "GraduationCertificate" => Map(
                ("FullNameEn", profile.FullNameEn),
                ("University", profile.University),
                ("Faculty", profile.Faculty),
                ("Major", profile.Major),
                ("GraduationYear", profile.GraduationYear?.ToString())),

            "MilitaryCertificate" => Map(
                ("FullNameAr", profile.FullNameAr),
                ("MilitaryStatus", profile.MilitaryStatus)),

            _ => new Dictionary<string, string>()
        };

    private static IReadOnlyDictionary<string, string> Map(params (string Key, string? Value)[] pairs) =>
        pairs
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value!);
}

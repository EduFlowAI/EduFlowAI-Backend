using AdmissionDocumentType = EduFlowAI.Admission.Domain.Enums.DocumentType;
using AdmissionGender = EduFlowAI.Admission.Domain.Enums.Gender;
using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Documents.Application.Services;

public sealed class ProgramRequirementReader(IAdmissionDbContext admissionDbContext)
    : IProgramRequirementReader
{
    public async Task<IReadOnlySet<DocumentType>> GetRequiredTypesAsync(
        Guid programId,
        string gender,
        CancellationToken cancellationToken)
    {
        AdmissionGender? parsedGender = Enum.TryParse<AdmissionGender>(gender, true, out var value)
            ? value
            : null;

        var types = await admissionDbContext.ProgramDocumentRequirements
            .AsNoTracking()
            .Where(requirement =>
                requirement.ProgramId == programId &&
                (requirement.RequiredForGender == null || requirement.RequiredForGender == parsedGender))
            .Select(requirement => requirement.DocumentType)
            .Distinct()
            .ToListAsync(cancellationToken);

        return types
            .Where(type => type != AdmissionDocumentType.None)
            .Select(type => (DocumentType)(int)type)
            .ToHashSet();
    }
}

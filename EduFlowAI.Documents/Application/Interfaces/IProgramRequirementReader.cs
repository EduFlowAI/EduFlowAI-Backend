using System;
using System.Collections.Generic;
using System.Linq;
using EduFlowAI.Admission.Domain.Enums;
//using EduFlowAI.Documents.Domain.Enums;


// Keep the namespace whatever you are currently using for the fake interface
namespace EduFlowAI.Admission.Application.Interfaces.Services;

// 1. The Disposable Fake Record
public record ProgramRequirementSet(
    Guid ProgramId,
    Gender ApplicantGender,
    IEnumerable<EduFlowAI.Documents.Domain.Enums.DocumentType> DocumentTypes);

// 2. Your Fake Interface
public interface IProgramRequirementReader
{
    Task<ProgramRequirementSet?> ResolveAsync(
        Guid programId,
        Gender applicantGender,
        CancellationToken cancellationToken = default);
}
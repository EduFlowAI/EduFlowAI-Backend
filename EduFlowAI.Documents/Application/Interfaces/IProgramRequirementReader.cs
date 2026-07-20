using EduFlowAI.Documents.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.Documents.Application.Interfaces
{
    public interface IProgramRequirementReader
    {
        Task<IReadOnlySet<DocumentType>> GetRequiredTypesAsync(
            Guid programId,
            string gender, // Used to check if the applicant is male for military certificates
            CancellationToken cancellationToken);
    }
}
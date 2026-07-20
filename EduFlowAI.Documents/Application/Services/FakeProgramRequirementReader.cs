using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.Documents.Infrastructure.Fakes
{
    public class FakeProgramRequirementReader : IProgramRequirementReader
    {
        public Task<IReadOnlySet<DocumentType>> GetRequiredTypesAsync(Guid programId, string gender, CancellationToken cancellationToken)
        {
            // Hardcoding the minimum required documents for the ITI MVP[cite: 3]
            var requirements = new HashSet<DocumentType>
            {
                DocumentType.NationalId,
                DocumentType.BirthCertificate,
                DocumentType.GraduationCertificate,
            };

            // Only add Military Certificate for male applicants[cite: 3]
            if (string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase))
            {
                requirements.Add(DocumentType.MilitaryCertificate);
            }

            return Task.FromResult<IReadOnlySet<DocumentType>>(requirements);
        }
    }
}
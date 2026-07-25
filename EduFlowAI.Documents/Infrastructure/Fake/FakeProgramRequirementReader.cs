using EduFlowAI.Admission.Domain.Enums;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// Assuming your temporary interface and record live in this namespace for now
using EduFlowAI.Admission.Application.Interfaces.Services;

namespace EduFlowAI.Documents.Infrastructure.Fakes
{
    public class FakeProgramRequirementReader : IProgramRequirementReader
    {
        public Task<ProgramRequirementSet?> ResolveAsync(
            Guid programId,
            Gender applicantGender,
            CancellationToken cancellationToken = default)
        {
            // Hardcoding the minimum required documents for the ITI MVP
            var requirements = new HashSet<DocumentType>
            {
                DocumentType.NationalId,
                DocumentType.BirthCertificate,
                DocumentType.GraduationCertificate,
            };

            // Only add Military Certificate for male applicants using Abdallah's enum
            if (applicantGender == Gender.Male)
            {
                requirements.Add(DocumentType.MilitaryCertificate);
            }

            // Wrap the requirements inside the exact record structure Abdallah's code expects
            var dummySet = new ProgramRequirementSet(
                programId,
                applicantGender,
                requirements
            );

            return Task.FromResult<ProgramRequirementSet?>(dummySet);
        }
    }
}
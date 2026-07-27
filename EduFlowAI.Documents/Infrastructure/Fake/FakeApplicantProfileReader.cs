using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.Admission.Domain.Enums;
//using EduFlowAI.Documents.Application.Interfaces;

namespace EduFlowAI.Documents.Infrastructure.Fakes;

public sealed class FakeApplicantProfileReader : IApplicantProfileReader
{
    public Task<Gender> GetApplicantGenderAsync(string userId, CancellationToken cancellationToken)
    {
        // Pretend the user is Male so Abdallah's requirement check doesn't crash
        return Task.FromResult(Gender.Male);
    }
}
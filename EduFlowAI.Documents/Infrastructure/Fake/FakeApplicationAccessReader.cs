using System;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.Documents.Application.Interfaces;

namespace EduFlowAI.Documents.Infrastructure.Fakes;

public sealed class FakeApplicationAccessReader : IApplicationAccessReader
{
    // A hardcoded ID so the fake consistently returns the same ProgramId
    private static readonly Guid DummyProgramId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Task<bool> CanUploadDocumentsAsync(Guid applicationId, string userId, CancellationToken cancellationToken)
    {
        // Always pretend the user owns the application and can upload
        return Task.FromResult(true);
    }

    public Task<Guid> GetProgramIdAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        // Return a dummy Program ID
        return Task.FromResult(DummyProgramId);
    }
}
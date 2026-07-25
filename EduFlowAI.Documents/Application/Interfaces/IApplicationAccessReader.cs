using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.Documents.Application.Interfaces;

public interface IApplicationAccessReader
{
    Task<bool> CanUploadDocumentsAsync(Guid applicationId, string userId, CancellationToken cancellationToken);

    Task<Guid> GetProgramIdAsync(Guid applicationId, CancellationToken cancellationToken);
}
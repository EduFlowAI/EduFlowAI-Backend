namespace EduFlowAI.Admission.Application.Interfaces.Services
{
    public interface IApplicationAcessReader
    {
        Task<bool> CanAccessDocumentsAsync(Guid applicationId, string userId, CancellationToken cancellationToken);

        Task<Guid> GetProgramIdAsync(Guid applicationId, CancellationToken cancellationToken);

        Task<Guid> GetApplicationIdForUserAsync(string userId, CancellationToken cancellationToken);
    }
}

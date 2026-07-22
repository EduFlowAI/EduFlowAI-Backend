namespace EduFlowAI.Admission.Application.Interfaces.Services
{
    public interface IApplicationAcessReader
    {
        Task<bool> CanUploadDocumentsAsync(Guid applicationId, string userId);
    }
}

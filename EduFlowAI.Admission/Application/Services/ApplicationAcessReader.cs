using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;

namespace EduFlowAI.Admission.Application.Services
{
    public class ApplicationAcessReader : IApplicationAcessReader
    {
        private readonly IApplicationRepository _applicationRepository;

        public ApplicationAcessReader(IApplicationRepository applicationRepository)
        {
            _applicationRepository = applicationRepository;
        }

        public async Task<bool> CanUploadDocumentsAsync(Guid applicationId, string userId)
        {
            var application = await _applicationRepository.GetFirstOrDefaultAsync(app => app.Id == applicationId);
            if(application == null)
            {
                throw new Exception("This application is not found");
            }

            if(application.ApplicantUserId != userId)
            {
                return false;
            }

            return true;    // This user owns this application
        }
    }
}

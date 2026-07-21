using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;

namespace EduFlowAI.Admission.Application.Services
{
    public class ApplicationStatusQueryService : IApplicationStatusQueryService
    {
        private readonly IApplicationRepository _applicationRepository;

        public ApplicationStatusQueryService(IApplicationRepository applicationRepository)
        {
            _applicationRepository = applicationRepository;
        }

        public async Task<ApplicationStatusDto?> GetApplicationStatusAsync(Guid applicationId)
        {
            var application = await _applicationRepository.GetFirstOrDefaultAsync(app => app.Id == applicationId);
            if(application == null)
            {
                return null;
            }

            return new ApplicationStatusDto
            {
                ApplicationId = applicationId,
                CurrentStatus = application.Status.ToString(),
                LastUpdatedAt = application.UpdatedAt,
                StatusMessage = "Status retrieved successfully."
            };
        }
    }
}

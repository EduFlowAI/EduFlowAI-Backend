using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Domain.Enums;

namespace EduFlowAI.Admission.Application.Interfaces.Services
{
    public interface IAssessmentSimulationService
    {
        /// <summary>
        /// Generates a simulated result for a specific application and stage.
        /// </summary>
        /// <param name="applicationId"></param>
        /// <param name="stage"></param>
        /// <returns></returns>
        Task<(bool isSuccess, string? ErrorMessage, SimulatedStageDto? Data)> SimulateStageAsync(Guid applicationId, SelectionStage stage);

        /// <summary>
        /// A helper method to fetch all simulated stages for a specific applicant.
        /// </summary>
        /// <param name="applicationId"></param>
        /// <returns></returns>
        Task<IEnumerable<SimulatedStageDto>> GetApplicantStagesAsync(Guid applicationId);
    }
}

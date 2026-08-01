using EduFlowAI.Admission.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Application.Interfaces.Services
{
    public interface IApplicationStatusQueryService
    {
        /// <summary>
        /// Retrieves the current status of an application by its ID
        /// </summary>
        /// <param name="applicationId"></param>
        /// <returns></returns>
        Task<ApplicationStatusDto?> GetApplicationStatusAsync(Guid applicationId);


        Task<ApplicationStatusDto?> GetCurrentApplicationStatusForApplicantAsync(string applicantUserId, CancellationToken cancellationToken = default);

        Task<ApplicationDashboardSummaryDto?> GetDashboardSummaryAsync(Guid applicationId);

        /// <summary>
        /// Retrieves full application details including preferences for the edit page
        /// </summary>
        /// <param name="applicationId"></param>
        /// <param name="applicantUserId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ApplicationDetailsDto?> GetApplicationDetailsAsync(Guid applicationId,
            string applicantUserId, CancellationToken cancellationToken = default);
    }
}

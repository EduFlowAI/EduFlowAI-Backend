using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Shared.Kernel.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Application.Interfaces.Services
{
    public interface IApplicationService
    {
        Task<Result<ApplicationDto>> CreateDraftApplicationAsync(string applicantUserId, ApplicationRequestDto request);

        /// <summary>
        /// Updates the preferences of a Draft application
        /// </summary>
        /// <param name="applicationId"></param>
        /// <param name="applicantUserId"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Result<ApplicationDetailsDto>> UpdateApplicationPreferencesAsync(Guid applicationId, string applicantUserId, UpdateApplicationPreferencesDto request, CancellationToken cancellationToken = default);
    }
}

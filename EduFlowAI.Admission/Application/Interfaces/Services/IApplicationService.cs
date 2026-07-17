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
    }
}

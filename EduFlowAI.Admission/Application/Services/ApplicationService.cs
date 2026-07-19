using AutoMapper;
using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Shared.Kernel.Common;
using System;


namespace EduFlowAI.Admission.Application.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IMapper _mapper;

        public ApplicationService(IApplicationRepository applicationRepository,
            IMapper mapper)
        {
            _applicationRepository = applicationRepository;
            _mapper = mapper;
        }

        public async Task<Result<ApplicationDto>> CreateDraftApplicationAsync(string applicantUserId, ApplicationRequestDto request)
        {
            if(request == null)
                return Result<ApplicationDto>.Failure(400, "Request cannot be null");

            if(string.IsNullOrWhiteSpace(applicantUserId))
                return Result<ApplicationDto>.Failure(400, "Applicant user ID cannot be null or empty");

            // 1. Business Rule Validation: One application per applicant and cycle
            bool hasExistingApp = await _applicationRepository.HasExistingApplicationAsync(applicantUserId, request.CycleId);

            if (hasExistingApp)
                return Result<ApplicationDto>.Failure(400, "You have already created application for this admission cycle.");

            // 2. Map and set Business Rules/Domain logic
            var application = _mapper.Map<Domain.Entities.Application>(request);
            application.Id = Guid.NewGuid();
            application.ApplicantUserId = applicantUserId;
            application.Status = ApplicationStatus.Draft;
            application.CreatedAt = DateTime.UtcNow;
            application.UpdatedAt = DateTime.UtcNow;

            // Ensure preferences get unique IDs and link to the application
            if(application.Preferences != null)
            {
                foreach(var pref in application.Preferences)
                {
                    pref.Id = Guid.NewGuid();
                    pref.ApplicationId = application.Id;
                }
            }

            // 3. Save to the database
            await _applicationRepository.AddAsync(application);
            await _applicationRepository.SaveChangesAsync();

            // 4. Map the saved entity back to a DTO to return to the Presentation layer
            var resultDto = _mapper.Map<ApplicationDto>(application);

            return Result<ApplicationDto>.Success(resultDto);
        }
    }
}

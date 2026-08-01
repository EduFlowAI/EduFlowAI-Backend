using AutoMapper;
using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
using System;


namespace EduFlowAI.Admission.Application.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IGenericRepository<TrackBranchOffering> _offeringRepository;
        private readonly IMapper _mapper;

        public ApplicationService(IApplicationRepository applicationRepository,
            IGenericRepository<TrackBranchOffering> offeringRepository,
            IMapper mapper)
        {
            _applicationRepository = applicationRepository;
            _offeringRepository = offeringRepository;
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

        public async Task<Result<ApplicationDetailsDto>> UpdateApplicationPreferencesAsync(Guid applicationId, string applicantUserId, UpdateApplicationPreferencesDto request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.Preferences == null || !request.Preferences.Any())
                return Result<ApplicationDetailsDto>.Failure(400, "Preferences list cannot be empty.");

            // 1. Fetch the application and include existing preferences
            var application = await _applicationRepository.GetFirstOrDefaultAsync(
                predicate: app => app.Id == applicationId && app.ApplicantUserId == applicantUserId,
                include: query => query.Include(a => a.Preferences)
            );

            if (application == null)
                return Result<ApplicationDetailsDto>.Failure(404, "Application not found or you are not authorized to modify it.");

            // 2. Business Rule: Only allow updates if the application is in Draft status
            if (application.Status != ApplicationStatus.Draft)
                return Result<ApplicationDetailsDto>.Failure(400, "You can only edit preferences while the application is in Draft status.");

            // 3. Extract the requested Track and Branch IDs to fetch their corresponding Offerings
            var requestedTrackIds = request.Preferences.Select(p => p.TrackId).Distinct().ToList();
            var requestedBranchIds = request.Preferences.Select(p => p.BranchId).Distinct().ToList();

            // 4. Fetch matching offerings for the current Cycle in a SINGLE query (Performance optimization - Avoids N+1)
            var availableOfferings = await _offeringRepository.GetAllAsync(o =>
                o.CycleId == application.CycleId &&
                requestedTrackIds.Contains(o.TrackId) &&
                requestedBranchIds.Contains(o.BranchId));

            // 5. Clear existing preferences
            application.Preferences?.Clear();
            if (application.Preferences == null)
            {
                application.Preferences = new List<ApplicationPreference>();
            }

            // 6. Map and validate new preferences
            foreach (var prefDto in request.Preferences)
            {
                // Find the specific offering that matches this Track and Branch combination
                var matchedOffering = availableOfferings?.FirstOrDefault(o =>
                    o.TrackId == prefDto.TrackId &&
                    o.BranchId == prefDto.BranchId);

                // If no matching offering is found, it means the user selected an invalid combination for this Cycle
                if (matchedOffering == null)
                {
                    return Result<ApplicationDetailsDto>.Failure(400, $"The selected Track and Branch combination is not available for this admission cycle.");
                }

                // Create the domain entity mapped to the resolved TrackBranchOfferingId
                var newPreference = new ApplicationPreference
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = application.Id,
                    TrackBranchOfferingId = matchedOffering.Id,
                    Rank = prefDto.Rank,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                application.Preferences.Add(newPreference);
            }

            application.UpdatedAt = DateTime.UtcNow;

            // 7. Save changes to the database
            await _applicationRepository.SaveChangesAsync();

            // 8. Map to response DTO
            var preferencesDto = request.Preferences.OrderBy(p => p.Rank).ToList();

            var resultDto = new ApplicationDetailsDto(
                application.Id,
                application.ApplicantUserId,
                application.CycleId,
                application.Cycle?.Label ?? string.Empty,
                application.Cycle?.DeadlineUtc ?? DateTimeOffset.MinValue,
                application.Status.ToString(),
                application.CreatedAt,
                application.UpdatedAt,
                preferencesDto
            );

            return Result<ApplicationDetailsDto>.Success(resultDto, 200, "Preferences updated successfully.");
        }
    }
}
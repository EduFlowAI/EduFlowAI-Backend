using AutoMapper;
using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Admission.Application.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IGenericRepository<TrackBranchOffering> _offeringRepository;
        private readonly IGenericRepository<ApplicationPreference> _preferenceRepository;
        private readonly IMapper _mapper;

        public ApplicationService(IApplicationRepository applicationRepository,
            IGenericRepository<TrackBranchOffering> offeringRepository,
            IGenericRepository<ApplicationPreference> preferenceRepository,
            IMapper mapper)
        {
            _applicationRepository = applicationRepository;
            _offeringRepository = offeringRepository;
            _preferenceRepository = preferenceRepository;
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
            #region Edge cases validations
            if (request == null || request.Preferences == null || !request.Preferences.Any())
                return Result<ApplicationDetailsDto>.Failure(400, "Preferences list cannot be empty.");

            
            var uniqueOfferingsCount = request.Preferences.Select(p => new { p.TrackId, p.BranchId }).Distinct().Count();
            if (uniqueOfferingsCount != request.Preferences.Count)
                return Result<ApplicationDetailsDto>.Failure(400, "You cannot select the same track and branch combination more than once.");

            var uniqueRanksCount = request.Preferences.Select(p => p.Rank).Distinct().Count();
            if (uniqueRanksCount != request.Preferences.Count)
                return Result<ApplicationDetailsDto>.Failure(400, "Ranks must be unique for each preference.");
            #endregion

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


            // 5. Robustly sync existing preferences instead of using .Clear()
            // First, map the incoming DTOs to the resolved database offerings
            var resolvedPreferences = new List<(TrackBranchOffering Offering, short Rank)>();

            foreach (var prefDto in request.Preferences)
            {
                var matchedOffering = availableOfferings?.FirstOrDefault(o =>
                    o.TrackId == prefDto.TrackId &&
                    o.BranchId == prefDto.BranchId);

                if (matchedOffering == null)
                {
                    return Result<ApplicationDetailsDto>.Failure(400, "The selected Track and Branch combination is not available for this admission cycle.");
                }

                resolvedPreferences.Add((matchedOffering, prefDto.Rank));
            }

            // A flag to track if any actual database changes are needed
            bool hasChanges = false;

            // 6. Remove old preferences that are NO LONGER in the new request
            var newOfferingIds = resolvedPreferences.Select(r => r.Offering.Id).ToList();
            var preferencesToRemove = application.Preferences?
                .Where(p => !newOfferingIds.Contains(p.TrackBranchOfferingId))
                .ToList() ?? new List<ApplicationPreference>();

            if (preferencesToRemove.Any())
            {
                hasChanges = true;
                foreach (var prefToRemove in preferencesToRemove)
                {
                    // 1. MUST remove from the parent collection to prevent Change Tracker confusion
                    application.Preferences?.Remove(prefToRemove);
                    // 2. Explicitly delete from the database
                    _preferenceRepository.Delete(prefToRemove);
                }
            }
            
            foreach (var item in resolvedPreferences)
            {
                var existingPref = application.Preferences?
                    .FirstOrDefault(p => p.TrackBranchOfferingId == item.Offering.Id);

                if(existingPref != null)
                {
                    // Only flag as changed and update if the rank actually changed!
                    // This completely avoids the ConcurrencyException on identical double-submits.
                    if (existingPref.Rank != item.Rank)
                    {
                        existingPref.Rank = item.Rank;
                        existingPref.UpdatedAt = DateTimeOffset.UtcNow; // Unified to DateTime
                        hasChanges = true;
                    }
                }
                else
                {
                    // Add as a completely new preference
                    var newPref = new ApplicationPreference
                    {
                        // Id = Guid.NewGuid(), --> XX => Don't set the ID manually; let EF Core handle it to change the entity state to Added.
                        // This avoids the ConcurrencyException on double-submits.
                        ApplicationId = application.Id,
                        TrackBranchOfferingId = item.Offering.Id,
                        Rank = item.Rank,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    application.Preferences?.Add(newPref);
                    hasChanges = true;
                }
            }

            // 8. Save changes to the database ONLY if something was actually modified
            if (hasChanges)
            {
                // Update the timestamp unconditionally as EF Core handles xmin automatically
                application.UpdatedAt = DateTimeOffset.UtcNow;
                await _applicationRepository.SaveChangesAsync();
            }

            // 9. Map to response DTO
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
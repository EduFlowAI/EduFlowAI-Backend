using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EduFlowAI.Admission.Application.Services
{
    public class AllocationService : IAllocationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IGenericRepository<TrackBranchOffering> _offeringRepository;
        private readonly IGenericRepository<EnrollmentTaskLookup> _taskLookupRepository;

        public AllocationService(
            IApplicationRepository applicationRepository,
            IGenericRepository<TrackBranchOffering> offeringRepository,
            IGenericRepository<EnrollmentTaskLookup> taskLookupRepository
            )
        {
            _applicationRepository = applicationRepository;
            _offeringRepository = offeringRepository;
            _taskLookupRepository = taskLookupRepository;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)>
            RunAllocationAsync(
                Guid cycleId,
                CancellationToken cancellationToken = default)
        {
            // =================================================================================
            // STEP 1: DATA GATHERING
            // =================================================================================
            // 1. Fetch all track offerings for the current cycle to know the capacities.
            var offerings = await _offeringRepository.GetAllAsync(
                predicate: o => o.CycleId == cycleId
            );
            if (offerings == null || !offerings.Any())
            {
                return (false, "No track offerings found for this cycle. Cannot run allocation.");
            }

            // Convert offerings to a Dictionary for O(1) fast lookups during the algorithm.
            // Key: TrackBranchOfferingId, Value: The offering entity itself (to track capacity).
            var offeringsDict = offerings.ToDictionary(o => o.Id, o => o);

            // 2. Fetch all applications eligible for allocation in this cycle.
            // Eligibility Rule: Application must be in 'AssessmentInProgress' state.
            var eligibleApplications = await _applicationRepository.GetAllAsync(
                predicate: a => a.CycleId == cycleId && a.Status == ApplicationStatus.AssessmentInProgress,
                include: query => query
                    .Include(a => a.Preferences.OrderBy(p => p.Rank)) // Fetch preferences in order
                    .Include(a => a.SimulatedStageResults)            // Fetch all exam results
            );

            if (eligibleApplications == null || !eligibleApplications.Any())
            {
                return (false, "No eligible applications found for allocation in this cycle.");
            }

            // =================================================================================
            // STEP 2: THE ALGORITHM (Scoring, Sorting, and Matching)
            // =================================================================================
            // 2.1: Build the Scorecards (Flatten the data for the algorithm)
            var scorecards = new List<ApplicantTrackScoreDto>();

            foreach (var app in eligibleApplications)
            {
                // Fetch Global Scores (TrackId is null)
                var engResult = app.SimulatedStageResults
                    .FirstOrDefault(s => s.Stage == SelectionStage.EnglishExamAndIq && s.TrackId == null);
                var softResult = app.SimulatedStageResults
                    .FirstOrDefault(s => s.Stage == SelectionStage.SoftSkillsInterview && s.TrackId == null);

                decimal engScore = engResult?.Score ?? 0;
                decimal softScore = softResult?.Score ?? 0;

                // Evaluate each preference independently
                foreach (var pref in app.Preferences)
                {
                    var trackId = pref.TrackBranchOffering.TrackId;

                    // Fetch Track-Specific Scores
                    var progResult = app.SimulatedStageResults
                        .FirstOrDefault(s => s.Stage == SelectionStage.ProgrammingExam && s.TrackId == trackId);
                    var techResult = app.SimulatedStageResults
                        .FirstOrDefault(s => s.Stage == SelectionStage.TechnicalInterview && s.TrackId == trackId);

                    // Business Rule: Must have passed the Programming Exam for this specific track
                    if (progResult == null || progResult.Result != StageResult.Passed)
                    {
                        continue; // Skip this preference, applicant failed the track's technical requirement
                    }

                    decimal progScore = progResult.Score ?? 0;
                    decimal techScore = techResult?.Score ?? 0;

                    // Calculate Total Weighted Score based on provided business rules
                    decimal totalScore = (engScore * 0.5m) + (progScore * 1.0m) + (techScore * 1.0m) + (softScore * 1.0m);

                    scorecards.Add(new ApplicantTrackScoreDto
                    {
                        ApplicationId = app.Id,
                        TrackBranchOfferingId = pref.TrackBranchOfferingId,
                        PreferenceRank = pref.Rank,
                        TotalWeightedScore = totalScore,
                        TechnicalExamScore = progScore,
                        TechnicalInterviewScore = techScore,
                        IsEligibleForTrack = true
                    });
                }
            }

            // 2.2: Sort the Scorecards (The Absolute Merit List)
            // Order: Total Score (DESC) -> Tech Exam (DESC) -> Tech Interview (DESC) -> Preference Rank (ASC)
            var meritList = scorecards
                .OrderByDescending(s => s.TotalWeightedScore)
                .ThenByDescending(s => s.TechnicalExamScore)
                .ThenByDescending(s => s.TechnicalInterviewScore)
                .ThenBy(s => s.PreferenceRank)
                .ToList();

            // 2.3: The Allocation Loop (Matching applicants to seats)
            // Keeps track of the current successful allocation for each applicant
            // Key: ApplicationId, Value: Tuple of (OfferingId, PreferenceRank)
            var currentAllocations = new Dictionary<Guid, (Guid OfferingId, short Rank)>();

            // Keeps track of remaining seats per track offering
            var remainingCapacities = offeringsDict.ToDictionary(k => k.Key, v => v.Value.Capacity);

            foreach (var scorecard in meritList)
            {
                var appId = scorecard.ApplicationId;
                var offeringId = scorecard.TrackBranchOfferingId;
                var currentRank = scorecard.PreferenceRank;

                // Check if the applicant is already admitted to a BETTER (lower number) preference
                if (currentAllocations.TryGetValue(appId, out var existingAllocation))
                {
                    if (existingAllocation.Rank <= currentRank)
                    {
                        // Applicant already got a better or equal preference, skip this lower preference scorecard
                        continue;
                    }
                }

                // Applicant is either not allocated yet, or allocated to a worse preference.
                // Check if the target track has available capacity.
                if (remainingCapacities[offeringId] > 0)
                {
                    // If they had a worse allocation previously, free up that seat for someone else
                    if (currentAllocations.ContainsKey(appId))
                    {
                        var previousOfferingId = currentAllocations[appId].OfferingId;
                        remainingCapacities[previousOfferingId]++;
                    }

                    // Admit applicant to the new, better track
                    currentAllocations[appId] = (offeringId, currentRank);
                    remainingCapacities[offeringId]--;
                }
            }

            // =================================================================================
            // STEP 3: STATE MUTATION & DATABASE SAVE
            // =================================================================================
            // 3.1: Identify who is eligible for waitlist (passed technicals but didn't get a seat)
            var eligibleForWaitlist = new HashSet<Guid>(scorecards.Select(s => s.ApplicationId));

            // 3.2: Fetch task templates once to bulk-assign them to admitted students efficiently
            var taskTemplates = await _taskLookupRepository.GetAllAsync(t => t.IsActive);

            // 3.3: Mutate the state of each application based on the allocation results
            foreach (var app in eligibleApplications)
            {
                if (currentAllocations.TryGetValue(app.Id, out var allocation))
                {
                    // Applicant successfully matched with a track
                    app.Status = ApplicationStatus.Admitted;

                    // Assign the final accepted track based on the algorithm's result
                    app.AcceptedTrackBranchOfferingId = allocation.OfferingId;

                    app.UpdatedAt = DateTimeOffset.UtcNow;

                    // Generate enrollment tasks for the admitted applicant
                    if (taskTemplates != null && taskTemplates.Any())
                    {
                        app.EnrollmentTasks ??= new List<ApplicationEnrollmentTask>();
                        foreach (var template in taskTemplates)
                        {
                            if (!app.EnrollmentTasks.Any(t => t.TaskId == template.Id))
                            {
                                app.EnrollmentTasks.Add(new ApplicationEnrollmentTask
                                {
                                    TaskId = template.Id,
                                    Status = EnrollmentTaskStatus.Pending,
                                    CreatedAt = DateTimeOffset.UtcNow,
                                    UpdatedAt = DateTimeOffset.UtcNow
                                });
                            }
                        }
                    }
                }
                else if (eligibleForWaitlist.Contains(app.Id))
                {
                    // Applicant passed technical requirements but no seats were available
                    app.Status = ApplicationStatus.Waitlisted;

                    // Ensure it is null in case they were previously admitted in a manual override
                    app.AcceptedTrackBranchOfferingId = null;
                    app.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    // Applicant did not pass the technical requirements
                    app.Status = ApplicationStatus.NotSelected;
                    app.AcceptedTrackBranchOfferingId = null;
                    app.UpdatedAt = DateTimeOffset.UtcNow;
                }

                _applicationRepository.Update(app);
            }

            // 3.4: Commit all changes to the database in a single transaction
            await _applicationRepository.SaveChangesAsync();

            // Calculate metrics for the response
            int admittedCount = currentAllocations.Count;
            int waitlistedCount = eligibleApplications.Count(a => eligibleForWaitlist.Contains(a.Id) && !currentAllocations.ContainsKey(a.Id));

            return (true, $"Allocation completed successfully. Admitted: {admittedCount}, Waitlisted: {waitlistedCount}.");
        }
    }
}

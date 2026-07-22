using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;

namespace EduFlowAI.Admission.Application.Services
{
    public class AssessmentSimulationService : IAssessmentSimulationService
    {
        private readonly IGenericRepository<SimulatedStageResult> _stageRepository;
        private readonly IApplicationRepository _applicationRepository;
        private readonly Random _random;

        public AssessmentSimulationService(IGenericRepository<SimulatedStageResult> stageRepository,
            IApplicationRepository applicationRepository)
        {
            _stageRepository = stageRepository;
            _applicationRepository = applicationRepository;
            _random = new Random();
        }

        public async Task<(bool isSuccess, string? ErrorMessage, SimulatedStageDto? Data)> SimulateStageAsync(Guid applicationId, SelectionStage stage)
        {
            // 1. Verify that the application exists
            var application = await _applicationRepository.GetFirstOrDefaultAsync(app => app.Id == applicationId);
            if(application == null)
            {
                return (false, "Application is not found", null);
            }

            // 2. Check if the stage has already been simulated for this application to avoid duplicates
            var existingStage = await _stageRepository.GetFirstOrDefaultAsync(s => s.ApplicationId == applicationId && s.Stage == stage);
            if(existingStage != null)
            {
                return(false, $"Stage '{stage}' has already been simulated for this application.", null);
            }

            /*
             * 3. Business Logic: Simulate the score
             * For simulation purposes, we generate a random score. 
             * Let's assume MaxScore is 100 for all exams for simplicity.
             */

            decimal maxScore = 100m;
            decimal simulatedScore = _random.Next(30, 101);

            // Determine if passed (e.g., Passing score is 60)
            var stageResult = simulatedScore >= 60 ? StageResult.Passed : StageResult.NotPassed;

            // 4. Create the Domain Entity
            var newStageResult = new SimulatedStageResult
            {
                ApplicationId = applicationId,
                Stage = stage,
                Score = simulatedScore,
                MaxScore = maxScore,
                Result = stageResult,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // 5. Save to Database
            await _stageRepository.AddAsync(newStageResult);
            await _stageRepository.SaveChangesAsync();

            // 6. Map to DTO and return
            var stageResultDto = new SimulatedStageDto
            {
                StageId = newStageResult.Id,
                StageType = newStageResult.Stage.ToString(),
                Score = newStageResult.Score,
                MaxScore = newStageResult.MaxScore,
                Result = newStageResult.Result.ToString(),
                UpdatedAt = newStageResult.UpdatedAt ?? newStageResult.CreatedAt
            };

            return (true, null, stageResultDto);
        }

        public async Task<IEnumerable<SimulatedStageDto>> GetApplicantStagesAsync(Guid applicationId)
        {
            var stages = await _stageRepository.GetAllAsync(s => s.ApplicationId == applicationId);
            if(stages == null)
            {
                return Enumerable.Empty<SimulatedStageDto>();
            }

            return stages.Select(s => new SimulatedStageDto
            {
                StageId = s.Id,
                StageType = s.Stage.ToString(),
                Score = s.Score,
                MaxScore = s.MaxScore,
                Result = s.Result.ToString(),
                UpdatedAt = s.UpdatedAt ?? s.CreatedAt
            }).ToList();
        }
    }
}

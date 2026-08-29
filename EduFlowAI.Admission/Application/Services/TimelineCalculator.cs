using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;

namespace EduFlowAI.Admission.Application.Services
{
    // Extracted Magic Strings to a Constant Class
    public static class PhaseStatus
    {
        public const string Pending = "Pending";
        public const string InProgress = "InProgress";
        public const string Passed = "Passed";
        public const string Failed = "Failed";
    }

    // Extracted Magic Numbers to a Constant Class
    public static class TimelinePercentage
    {
        public const int ApplicationPhase = 0;
        public const int EligibilityPhase = 16;
        public const int VerificationPhase = 33;
        public const int EnglishPhase = 50;
        public const int TechnicalPhase = 66;
        public const int InterviewPhase = 83;
        public const int FinalPhase = 100;
    }

    public class TimelineCalculator : ITimelineCalculator
    {
        public TimelineStateResult Calculate(ApplicationStatus status, IEnumerable<SimulatedStageResult> stages, EligibilityResult? eligibilityResult)
        {
            // 1. Initialize default states
            int percentage = TimelinePercentage.ApplicationPhase;
            string app = PhaseStatus.Pending, elig = PhaseStatus.Pending, verif = PhaseStatus.Pending,
                   eng = PhaseStatus.Pending, tech = PhaseStatus.Pending, intv = PhaseStatus.Pending, final = PhaseStatus.Pending;

            // 2. Application Phase
            if (status == ApplicationStatus.Draft || status == ApplicationStatus.None)
            {
                return new TimelineStateResult { Percentage = TimelinePercentage.ApplicationPhase, AppStatus = PhaseStatus.InProgress };
            }
            app = PhaseStatus.Passed;

            // 3. Eligibility Phase
            if (eligibilityResult != null)
            {
                elig = eligibilityResult.Passed ? PhaseStatus.Passed : PhaseStatus.Failed;
                percentage = TimelinePercentage.EligibilityPhase;

                if (!eligibilityResult.Passed)
                {
                    return CreateResult(percentage, app, elig, verif, eng, tech, intv, final);
                }
            }
            else if (status == ApplicationStatus.EligibilityFailed)
            {
                return CreateResult(TimelinePercentage.EligibilityPhase, app, PhaseStatus.Failed, verif, eng, tech, intv, final);
            }
            else
            {
                return CreateResult(TimelinePercentage.EligibilityPhase, app, PhaseStatus.InProgress, verif, eng, tech, intv, final);
            }

            // 4. Verification Phase
            if (status == ApplicationStatus.DocumentsRequired ||
                status == ApplicationStatus.UnderDocumentVerification ||
                status == ApplicationStatus.NeedsHumanReview)
            {
                return CreateResult(TimelinePercentage.VerificationPhase, app, elig, PhaseStatus.InProgress, eng, tech, intv, final);
            }
            else if (status == ApplicationStatus.DocumentRejected)
            {
                return CreateResult(TimelinePercentage.VerificationPhase, app, elig, PhaseStatus.Failed, eng, tech, intv, final);
            }
            verif = PhaseStatus.Passed;

            // 5. Assessment Phases
            var englishStage = stages.FirstOrDefault(s => s.Stage == SelectionStage.EnglishExamAndIq);
            var technicalStage = stages.FirstOrDefault(s => s.Stage == SelectionStage.ProgrammingExam || s.Stage == SelectionStage.TechnicalInterview);
            var interviewStage = stages.FirstOrDefault(s => s.Stage == SelectionStage.SoftSkillsInterview);

            // English Stage Evaluation
            if (englishStage != null)
            {
                eng = englishStage.Result == StageResult.Passed ? PhaseStatus.Passed :
                     (englishStage.Result == StageResult.NotPassed ? PhaseStatus.Failed : PhaseStatus.InProgress);
                percentage = TimelinePercentage.EnglishPhase;
            }
            else if (status == ApplicationStatus.AssessmentInProgress)
            {
                eng = PhaseStatus.InProgress;
                percentage = TimelinePercentage.EnglishPhase;
            }

            // Technical Stage Evaluation
            if (eng == PhaseStatus.Passed)
            {
                if (technicalStage != null)
                {
                    tech = technicalStage.Result == StageResult.Passed ? PhaseStatus.Passed :
                          (technicalStage.Result == StageResult.NotPassed ? PhaseStatus.Failed : PhaseStatus.InProgress);
                    percentage = TimelinePercentage.TechnicalPhase;
                }
                else if (englishStage?.Result == StageResult.Passed)
                {
                    tech = PhaseStatus.InProgress;
                    percentage = TimelinePercentage.TechnicalPhase;
                }
            }

            // Interviews Stage Evaluation
            if (tech == PhaseStatus.Passed)
            {
                if (interviewStage != null)
                {
                    intv = interviewStage.Result == StageResult.Passed ? PhaseStatus.Passed :
                          (interviewStage.Result == StageResult.NotPassed ? PhaseStatus.Failed : PhaseStatus.InProgress);
                    percentage = TimelinePercentage.InterviewPhase;
                }
                else if (technicalStage?.Result == StageResult.Passed)
                {
                    intv = PhaseStatus.InProgress;
                    percentage = TimelinePercentage.InterviewPhase;
                }
            }

            // 6. Final Result Phase
            if (status == ApplicationStatus.Admitted)
            {
                final = PhaseStatus.Passed;
                percentage = TimelinePercentage.FinalPhase;
            }
            else if (status == ApplicationStatus.Waitlisted)
            {
                final = PhaseStatus.InProgress;
                percentage = TimelinePercentage.FinalPhase;
            }
            else if (status == ApplicationStatus.NotSelected)
            {
                final = PhaseStatus.Failed;
                percentage = TimelinePercentage.FinalPhase;
            }

            return CreateResult(percentage, app, elig, verif, eng, tech, intv, final);
        }

        // ====================== Helper Methods ======================
        // Helper method to keep code clean and avoid repetitive object initialization
        private TimelineStateResult CreateResult(int percentage, string app, string elig, string verif, string eng, string tech, string intv, string final)
        {
            return new TimelineStateResult
            {
                Percentage = percentage,
                AppStatus = app,
                EligStatus = elig,
                VerifStatus = verif,
                EngStatus = eng,
                TechStatus = tech,
                IntStatus = intv,
                FinalStatus = final
            };
        }
    }
}

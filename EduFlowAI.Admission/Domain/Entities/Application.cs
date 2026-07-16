using EduFlowAI.Admission.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Domain.Entities
{
    public class Application
    {
        public Guid Id { get; set; }

        public string ApplicantUserId { get; set; }
        public Guid CycleId { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
        public DateTimeOffset? SubmittedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public uint RowVersion { get; set; }

        // Navigation properties
        public AdmissionCycle Cycle { get; set; } = null!;
        public ICollection<ApplicationPreference> Preferences { get; set; } = new List<ApplicationPreference>();
        public EligibilityResult? EligibilityResult { get; set; }
        public ICollection<SimulatedStageResult> SimulatedStageResults { get; set; } = new List<SimulatedStageResult>();
    }
}

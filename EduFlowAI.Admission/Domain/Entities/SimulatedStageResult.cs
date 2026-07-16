using EduFlowAI.Admission.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Domain.Entities
{
    public class SimulatedStageResult
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }

        public SelectionStage Stage { get; set; }
        public StageResult Result { get; set; }
        public decimal? Score { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Application Application { get; set; } = null!;
    }
}

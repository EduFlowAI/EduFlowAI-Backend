using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Application.DTOs
{
    public record SimulatedStageDto
    {
        public Guid StageId { get; set; }
        public required string StageType { get; set; }
        public decimal? Score { get; set; }
        public decimal MaxScore { get; set; }
        public required string Result { get; set; }

        // Date when the score was recorded
        public DateTimeOffset UpdatedAt { get; set; }
    }
}

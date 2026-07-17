using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Application.DTOs
{
    public record ApplicationDto(
        Guid Id, string ApplicantUserId, Guid CycleId, string Status,
        List<PreferenceDto> Preferences
    );

    public record PreferenceDto(Guid TrackBranchOfferingId, short Rank);
}

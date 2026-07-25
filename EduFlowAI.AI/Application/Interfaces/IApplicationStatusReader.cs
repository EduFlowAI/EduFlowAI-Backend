using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

// Port implemented by the Admission module, so this module reads application status without
// depending on it (same arrangement as IOfferedTrackReader). Null means the applicant has no
// application yet.
public interface IApplicationStatusReader
{
    Task<ApplicantStatusDto?> GetCurrentStatusAsync(string userId, CancellationToken cancellationToken = default);
}

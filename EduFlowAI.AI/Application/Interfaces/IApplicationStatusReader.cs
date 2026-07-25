using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

// Implemented by the Admission module. Null when the applicant has no application.
public interface IApplicationStatusReader
{
    Task<ApplicantStatusDto?> GetCurrentStatusAsync(string userId, CancellationToken cancellationToken = default);
}

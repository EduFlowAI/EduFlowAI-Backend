using EduFlowAI.Identity.Application.DTOs;

namespace EduFlowAI.Identity.Application.Interfaces;

public interface IProfileService
{
    Task<IdentityOperationResult<ProfileResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult<ProfileResponse>> UpdateAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}

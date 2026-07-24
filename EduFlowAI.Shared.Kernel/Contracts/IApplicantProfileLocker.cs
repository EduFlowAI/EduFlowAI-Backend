namespace EduFlowAI.Shared.Kernel.Contracts;

public interface IApplicantProfileLocker
{
    Task LockAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
namespace EduFlowAI.Shared.Kernel.Contracts;

public interface IApplicantProfileLocker
{
    Task LockAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

using EduFlowAI.Admission.Domain.Enums;

public interface IApplicantProfileReader
{
    // Karim needs to implement this by querying his Identity database
    Task<Gender> GetApplicantGenderAsync(string userId, CancellationToken cancellationToken);
}
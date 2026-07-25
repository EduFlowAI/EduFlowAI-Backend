namespace EduFlowAI.AI.Application.DTOs;

public class StatusAnswerDto
{
    public string Answer { get; set; } = string.Empty;

    // Set for a guest caller; drives the frontend's login-required state.
    public bool RequiresSignIn { get; set; }
}

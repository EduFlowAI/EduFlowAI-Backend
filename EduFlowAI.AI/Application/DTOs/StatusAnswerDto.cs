namespace EduFlowAI.AI.Application.DTOs;

public class StatusAnswerDto
{
    public string Answer { get; set; } = string.Empty;

    // True when the caller is a guest - the frontend shows its login-required state.
    public bool RequiresSignIn { get; set; }
}

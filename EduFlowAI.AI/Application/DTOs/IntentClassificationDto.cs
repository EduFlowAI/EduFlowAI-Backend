namespace EduFlowAI.AI.Application.DTOs;

public class IntentClassificationDto
{
    public string PrimaryIntent { get; set; } = string.Empty;  // "knowledge", "status", "recommendation"
    public decimal Confidence { get; set; }  // 0.0 to 1.0
    public string RoutedTo { get; set; } = string.Empty;
    public bool RequiresClarification { get; set; }
}
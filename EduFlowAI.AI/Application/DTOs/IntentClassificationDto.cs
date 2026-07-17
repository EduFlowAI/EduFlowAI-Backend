using System.Text.Json.Serialization;

namespace EduFlowAI.AI.Application.DTOs;

public class IntentClassificationDto
{
    [JsonPropertyName("primaryIntent")]
    public string PrimaryIntent { get; set; } = string.Empty;  // "knowledge", "status", "recommendation"

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }  // 0.0 to 1.0

    // Derived from PrimaryIntent by the classifier; never read from the model's JSON.
    [JsonIgnore]
    public string RoutedTo { get; set; } = string.Empty;

    [JsonPropertyName("requiresClarification")]
    public bool RequiresClarification { get; set; }
}

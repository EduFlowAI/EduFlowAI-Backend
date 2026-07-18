using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EduFlowAI.AI.Application.DTOs;

public class IntentClassificationDto
{
    // Every intent found in the message, ranked most likely first. A single-intent message
    // is a list of one, so callers can always iterate this.
    [JsonPropertyName("intents")]
    public List<DetectedIntentDto> Intents { get; set; } = new();

    // The top-ranked intent. Kept so a caller that handles one intent still works.
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

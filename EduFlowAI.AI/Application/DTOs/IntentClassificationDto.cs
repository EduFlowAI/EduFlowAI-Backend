using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EduFlowAI.AI.Application.DTOs;

public class IntentClassificationDto
{
    // Every intent found, ranked most likely first. A single-intent message is a list of one.
    [JsonPropertyName("intents")]
    public List<DetectedIntentDto> Intents { get; set; } = new();

    // The top-ranked intent. Kept so a caller that handles one intent still works.
    [JsonPropertyName("primaryIntent")]
    public string PrimaryIntent { get; set; } = string.Empty;  // knowledge, application_status, document_status, recommendation

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }  // 0.0 to 1.0

    // The language the router detected the message is written in ("ar"/"en"), or null when it
    // supplied nothing usable - the assistant then falls back to the letter-count heuristic / UI toggle.
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    // A self-contained English query the router wrote for the knowledge lookup, or null when it supplied
    // nothing usable - retrieval then falls back to the raw message joined with the recent turns.
    [JsonPropertyName("searchQuery")]
    public string? SearchQuery { get; set; }

    // Derived from PrimaryIntent by the classifier; never read from the model's JSON.
    [JsonIgnore]
    public string RoutedTo { get; set; } = string.Empty;

    [JsonPropertyName("requiresClarification")]
    public bool RequiresClarification { get; set; }
}

namespace EduFlowAI.AI.Infrastructure.Options;

public class IntentClassificationOptions
{
    public const string SectionName = "IntentClassification";

    /// <summary>
    /// Below this confidence the router asks the user to clarify instead of guessing and
    /// dispatching to the wrong agent. Raise it to be more cautious, lower it to route more
    /// aggressively.
    /// </summary>
    public decimal MinConfidence { get; set; } = 0.6m;
}

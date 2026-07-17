using System;

namespace EduFlowAI.AI.Infrastructure.Services;

public class ChatSessionOptions
{
    public const string SectionName = "ChatSession";

    /// <summary>How long a session may stay idle before it is treated as expired and evicted.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Maximum turns retained per session; oldest turns are trimmed beyond this. 0 = unlimited.</summary>
    public int MaxHistoryTurns { get; set; } = 50;
}

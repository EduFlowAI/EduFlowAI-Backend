using System;
using System.Collections.Generic;
using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

public interface ISessionManager
{
    ChatSessionDto CreateSession(Guid userId);
    ChatSessionDto? GetSession(string sessionId);

    /// <summary>Appends a turn. Returns false if the session is missing or expired (safe no-op).</summary>
    bool AddTurn(string sessionId, ConversationTurnDto turn);

    List<ConversationTurnDto> GetHistory(string sessionId, int lastN = 3);
    void EndSession(string sessionId);
}
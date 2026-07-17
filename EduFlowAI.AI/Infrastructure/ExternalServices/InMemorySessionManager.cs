using System;
using System.Collections.Generic;
using System.Linq;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Infrastructure.Services;

public class InMemorySessionManager : ISessionManager
{
    private readonly Dictionary<string, ChatSessionDto> _sessions = new();
    private readonly object _lockObject = new object();

    public ChatSessionDto CreateSession(Guid userId)
    {
        lock (_lockObject)
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new ChatSessionDto
            {
                SessionId = sessionId,
                UserId = userId
            };

            _sessions[sessionId] = session;
            return session;
        }
    }

    public ChatSessionDto? GetSession(string sessionId)
    {
        lock (_lockObject)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
    }

    public void AddTurn(string sessionId, ConversationTurnDto turn)
    {
        lock (_lockObject)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException($"Session with ID {sessionId} was not found.");

            session.History.Add(turn);
            session.LastActivityAt = DateTimeOffset.UtcNow;
        }
    }

    public List<ConversationTurnDto> GetHistory(string sessionId, int lastN = 3)
    {
        lock (_lockObject)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return new List<ConversationTurnDto>();

            return session.History
                .Skip(Math.Max(0, session.History.Count - lastN))
                .ToList();
        }
    }

    public void EndSession(string sessionId)
    {
        lock (_lockObject)
        {
            _sessions.Remove(sessionId);
        }
    }
}
using System;
using System.Collections.Generic;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Infrastructure.Services;

namespace EduFlowAI.AI.Tests;

public class InMemorySessionManagerTests
{
    private static ConversationTurnDto Turn(string q) => new() { Question = q, Answer = "a" };

    [Fact]
    public void CreateSession_ReturnsSessionWithIdUserAndEmptyHistory()
    {
        var mgr = new InMemorySessionManager();
        var userId = Guid.NewGuid();

        var session = mgr.CreateSession(userId);

        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Equal(userId, session.UserId);
        Assert.Empty(session.History);
    }

    [Fact]
    public void GetSession_UnknownId_ReturnsNull()
    {
        var mgr = new InMemorySessionManager();
        Assert.Null(mgr.GetSession("does-not-exist"));
    }

    [Fact]
    public void AddTurn_AppendsToHistory()
    {
        var mgr = new InMemorySessionManager();
        var session = mgr.CreateSession(Guid.NewGuid());

        mgr.AddTurn(session.SessionId, Turn("Q1"));

        var history = mgr.GetHistory(session.SessionId, lastN: 10);
        Assert.Single(history);
        Assert.Equal("Q1", history[0].Question);
    }

    [Fact]
    public void GetHistory_ReturnsLastNInChronologicalOrder()
    {
        var mgr = new InMemorySessionManager();
        var session = mgr.CreateSession(Guid.NewGuid());
        for (int i = 1; i <= 5; i++)
            mgr.AddTurn(session.SessionId, Turn($"Q{i}"));

        var last3 = mgr.GetHistory(session.SessionId, lastN: 3);

        Assert.Equal(3, last3.Count);
        Assert.Equal("Q3", last3[0].Question);
        Assert.Equal("Q4", last3[1].Question);
        Assert.Equal("Q5", last3[2].Question);
    }

    [Fact]
    public void GetHistory_LastNLargerThanCount_ReturnsAllTurns()
    {
        var mgr = new InMemorySessionManager();
        var session = mgr.CreateSession(Guid.NewGuid());
        mgr.AddTurn(session.SessionId, Turn("only"));

        Assert.Single(mgr.GetHistory(session.SessionId, lastN: 100));
    }

    [Fact]
    public void GetHistory_UnknownSession_ReturnsEmpty()
    {
        var mgr = new InMemorySessionManager();
        Assert.Empty(mgr.GetHistory("missing", lastN: 3));
    }

    [Fact]
    public void EndSession_RemovesSession()
    {
        var mgr = new InMemorySessionManager();
        var session = mgr.CreateSession(Guid.NewGuid());

        mgr.EndSession(session.SessionId);

        Assert.Null(mgr.GetSession(session.SessionId));
    }

    [Fact]
    public void EndSession_CalledTwice_DoesNotThrow()
    {
        var mgr = new InMemorySessionManager();
        var session = mgr.CreateSession(Guid.NewGuid());

        mgr.EndSession(session.SessionId);
        mgr.EndSession(session.SessionId); // second removal is a no-op

        Assert.Null(mgr.GetSession(session.SessionId));
    }

    [Fact]
    public void SeparateSessions_KeepIndependentHistories()
    {
        var mgr = new InMemorySessionManager();
        var a = mgr.CreateSession(Guid.NewGuid());
        var b = mgr.CreateSession(Guid.NewGuid());

        mgr.AddTurn(a.SessionId, Turn("a-only"));

        Assert.Single(mgr.GetHistory(a.SessionId, lastN: 10));
        Assert.Empty(mgr.GetHistory(b.SessionId, lastN: 10));
    }

    [Fact]
    public void AddTurn_UnknownSession_ThrowsToday()
    {
        // Documents CURRENT behavior. Task 7 changes this to a safe no-op (returns false);
        // this assertion will be flipped when that hardening lands.
        var mgr = new InMemorySessionManager();
        Assert.Throws<KeyNotFoundException>(() => mgr.AddTurn("missing", Turn("x")));
    }
}

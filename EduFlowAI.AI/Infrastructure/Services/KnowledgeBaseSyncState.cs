using System.Threading;

namespace EduFlowAI.AI.Infrastructure.Services;

/// <summary>
/// Tracks whether a knowledge-base re-sync is in progress. During a sync all chunks are deleted
/// and rebuilt, so retrieval would return partial results — the chat endpoint reports the
/// assistant as temporarily unavailable instead (team plan Part 8).
/// Singleton; only one sync may run at a time.
/// </summary>
public class KnowledgeBaseSyncState
{
    private int _syncing; // 0 = idle, 1 = syncing

    public bool IsSyncing => Volatile.Read(ref _syncing) == 1;

    /// <summary>Atomically claims the sync slot. Returns false if a sync is already running.</summary>
    public bool TryBeginSync() => Interlocked.CompareExchange(ref _syncing, 1, 0) == 0;

    public void EndSync() => Volatile.Write(ref _syncing, 0);
}

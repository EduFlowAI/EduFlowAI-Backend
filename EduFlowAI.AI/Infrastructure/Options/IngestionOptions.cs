namespace EduFlowAI.AI.Infrastructure.Options;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    // Transcribe with the model instead of local iText extraction.
    public bool UseAiExtraction { get; set; } = true;

    // Base64 inflates the payload by roughly a third, so stay well under the request limit.
    public int MaxInlineBytesForAi { get; set; } = 8 * 1024 * 1024;

    public long MaxUploadBytes { get; set; } = 20L * 1024 * 1024;

    // The indexing queue is in-memory, so a restart strands anything left Pending or Indexing.
    // Set false in any second host that shares this module, or both would index the same documents.
    public bool RequeueStrandedOnStartup { get; set; } = true;

    public int MaxChunkChars { get; set; } = 1200;

    public int ChunkOverlapChars { get; set; } = 200;
}

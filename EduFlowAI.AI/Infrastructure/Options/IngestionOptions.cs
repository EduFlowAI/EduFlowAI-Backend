namespace EduFlowAI.AI.Infrastructure.Options;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>
    /// Prefer AI transcription over local iText extraction. iText corrupts ligatures
    /// ("proficiency" -> "pro?ciency") and interleaves multi-column layouts; the model preserves
    /// reading order, tables and characters. Set false to force the offline path.
    /// </summary>
    public bool UseAiExtraction { get; set; } = true;

    /// <summary>
    /// Largest document sent inline to the model. Base64 inflates payloads by ~33%, so this stays
    /// well under the request limit; bigger files fall back to local extraction.
    /// </summary>
    public int MaxInlineBytesForAi { get; set; } = 8 * 1024 * 1024;

    /// <summary>Target maximum characters per chunk.</summary>
    public int MaxChunkChars { get; set; } = 1200;

    /// <summary>Characters of trailing context repeated at the start of the next chunk.</summary>
    public int ChunkOverlapChars { get; set; } = 200;
}

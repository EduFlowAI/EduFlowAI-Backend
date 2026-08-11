namespace EduFlowAI.AI.Infrastructure.Options;

public class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    // How many chunks are handed to the answer prompt. Five was too few to cover a question whose
    // answer is spread over a long reference document (e.g. the per-branch track lists).
    public int MaxContextChunks { get; set; } = 10;

    // Share of the budget reserved for literal keyword hits, so a proper noun cannot be crowded out
    // by nearer-but-less-relevant vector neighbours.
    public int MaxKeywordChunks { get; set; } = 4;
}

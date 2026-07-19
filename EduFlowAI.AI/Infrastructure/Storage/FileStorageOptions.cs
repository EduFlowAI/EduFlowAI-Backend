namespace EduFlowAI.AI.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    // Relative values resolve against the app base directory.
    public string Root { get; set; } = "protected-files";
}

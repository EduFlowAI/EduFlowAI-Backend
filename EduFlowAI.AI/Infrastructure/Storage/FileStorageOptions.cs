namespace EduFlowAI.AI.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Root folder for protected files. A relative value resolves against the app base directory.
    /// </summary>
    public string Root { get; set; } = "protected-files";
}

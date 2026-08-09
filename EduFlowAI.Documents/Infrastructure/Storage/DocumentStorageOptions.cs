namespace EduFlowAI.Documents.Infrastructure.Storage;

public class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    /// <summary>
    /// Absolute path to the secure document storage directory.
    /// Both the API and Worker must point to the same folder.
    /// </summary>
    public string Root { get; set; } = @"D:\EduFlowAI\SecureStorage";
}

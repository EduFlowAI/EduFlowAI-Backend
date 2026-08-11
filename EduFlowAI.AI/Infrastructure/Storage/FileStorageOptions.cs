namespace EduFlowAI.AI.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    // Where uploaded knowledge-base files are kept. Absolute values are used as given - set this to a
    // mounted volume when hosting, because a container's own disk does not survive a redeploy.
    // Leave it empty and a per-user location outside the build output is used, so uploads survive a
    // rebuild. A relative value is still resolved against the build output, for callers that want that.
    public string Root { get; set; } = string.Empty;
}

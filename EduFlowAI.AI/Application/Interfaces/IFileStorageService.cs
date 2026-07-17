using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Application.Interfaces;

/// <summary>
/// Protected file storage for uploaded documents. Callers work with opaque relative keys
/// (e.g. "knowledge-base/{id}.pdf"); the implementation owns the physical location.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Persists the content under <paramref name="relativeKey"/> and returns that key.</summary>
    Task<string> SaveAsync(Stream content, string relativeKey, CancellationToken cancellationToken = default);

    /// <summary>Opens a readable stream for a stored key. Throws if the key does not exist.</summary>
    Task<Stream> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes the stored file if present; a missing key is a no-op.</summary>
    Task DeleteAsync(string relativeKey, CancellationToken cancellationToken = default);

    bool Exists(string relativeKey);
}

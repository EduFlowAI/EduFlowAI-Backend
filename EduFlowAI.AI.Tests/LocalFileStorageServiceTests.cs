using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EduFlowAI.AI.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Tests;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorageService _storage;

    public LocalFileStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "eduflow-storage-tests", Guid.NewGuid().ToString("N"));
        _storage = new LocalFileStorageService(Options.Create(new FileStorageOptions { Root = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveThenOpenRead_RoundTripsContent()
    {
        var key = "knowledge-base/doc-1.pdf";
        await _storage.SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes("%PDF hello")), key);

        Assert.True(_storage.Exists(key));

        await using var read = await _storage.OpenReadAsync(key);
        using var reader = new StreamReader(read);
        Assert.Equal("%PDF hello", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Delete_RemovesFile()
    {
        var key = "knowledge-base/doc-2.pdf";
        await _storage.SaveAsync(new MemoryStream(new byte[] { 1, 2, 3 }), key);
        Assert.True(_storage.Exists(key));

        await _storage.DeleteAsync(key);

        Assert.False(_storage.Exists(key));
    }

    [Fact]
    public async Task Delete_MissingKey_IsNoOp()
    {
        await _storage.DeleteAsync("knowledge-base/never-existed.pdf"); // must not throw
    }

    [Fact]
    public async Task Save_PathTraversalKey_IsRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _storage.SaveAsync(new MemoryStream(new byte[] { 1 }), "../escape.pdf"));
    }
}

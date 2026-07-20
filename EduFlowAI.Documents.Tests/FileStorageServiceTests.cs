using EduFlowAI.Documents.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EduFlowAI.Documents.Tests;

public sealed class FileStorageServiceTests
{
    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var service = new FileStorageService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveFileAsync(FormFile([1, 2, 3], "document.exe")));
    }

    [Fact]
    public async Task Invalid_magic_bytes_are_rejected()
    {
        var service = new FileStorageService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveFileAsync(FormFile([1, 2, 3], "document.pdf")));
    }

    [Fact]
    public async Task Oversized_file_is_rejected()
    {
        var service = new FileStorageService();
        var bytes = new byte[10 * 1024 * 1024 + 1];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveFileAsync(FormFile(bytes, "document.pdf")));
    }

    [Fact]
    public async Task Valid_file_can_be_read_and_deleted_without_exposing_a_path()
    {
        var service = new FileStorageService();
        var key = await service.SaveFileAsync(FormFile([0x25, 0x50, 0x44, 0x46, 1], "document.pdf"));

        Assert.DoesNotContain(Path.DirectorySeparatorChar, key);
        await using (var stream = await service.OpenReadAsync(key))
        {
            Assert.Equal(5, stream.Length);
        }
        await service.DeleteAsync(key);
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.OpenReadAsync(key));
    }

    private static IFormFile FormFile(byte[] bytes, string name) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
}

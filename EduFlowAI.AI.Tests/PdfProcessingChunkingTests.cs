using System.Linq;
using EduFlowAI.AI.Infrastructure.ExternalServices;

namespace EduFlowAI.AI.Tests;

public class PdfProcessingChunkingTests
{
    [Fact]
    public void ShortText_ProducesSingleTrimmedChunk()
    {
        var chunks = PdfProcessingService.SliceIntoChunks("hello world", size: 1000, overlap: 200);

        Assert.Single(chunks);
        Assert.Equal("hello world", chunks[0]);
    }

    [Fact]
    public void LongText_ProducesOverlappingWindows()
    {
        var text = new string('a', 2500);

        var chunks = PdfProcessingService.SliceIntoChunks(text, size: 1000, overlap: 200);

        // step = 800 -> starts at 0, 800, 1600, 2400 => 4 windows
        Assert.Equal(4, chunks.Count);
        Assert.Equal(1000, chunks[0].Length);
        Assert.Equal(1000, chunks[1].Length);
    }

    [Fact]
    public void WhitespaceOnlyText_ProducesNoChunks()
    {
        Assert.Empty(PdfProcessingService.SliceIntoChunks("   \n\t  ", size: 1000, overlap: 200));
    }

    [Fact]
    public void EmptyOrNull_ProducesNoChunks()
    {
        Assert.Empty(PdfProcessingService.SliceIntoChunks("", size: 1000, overlap: 200));
        Assert.Empty(PdfProcessingService.SliceIntoChunks(null!, size: 1000, overlap: 200));
    }

    [Fact]
    public void OverlapGreaterThanOrEqualToSize_DoesNotStallAndStillChunks()
    {
        // A bad overlap (>= size) must not cause an infinite loop; it is corrected internally.
        var text = new string('x', 50);

        var chunks = PdfProcessingService.SliceIntoChunks(text, size: 10, overlap: 20);

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.True(c.Length > 0));
    }

    [Fact]
    public void NewlinesAreNormalizedToSpaces()
    {
        var chunks = PdfProcessingService.SliceIntoChunks("line one\r\nline two\nline three", size: 1000, overlap: 200);

        Assert.Single(chunks);
        Assert.DoesNotContain('\n', chunks[0]);
        Assert.Contains("line one line two line three", chunks[0]);
    }
}

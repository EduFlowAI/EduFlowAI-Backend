using System;
using System.IO;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace EduFlowAI.AI.Infrastructure.ExternalServices;

/// <summary>
/// Local (offline) PDF text extraction with iText. This is the fallback path: it is fast and free
/// but loses ligatures and can interleave multi-column layouts, so
/// <see cref="Processing.DocumentTextExtractor"/> prefers AI transcription and only falls back
/// here for oversized files or transcription failures.
/// </summary>
public class PdfProcessingService
{
    /// <summary>Extracts raw text from every page of a PDF stream.</summary>
    public string ExtractRawText(Stream pdfStream)
    {
        if (pdfStream == null)
            throw new ArgumentException("The provided PDF file stream is null.", nameof(pdfStream));

        var textBuilder = new StringBuilder();

        if (pdfStream.CanSeek)
            pdfStream.Position = 0;

        using var pdfReader = new PdfReader(pdfStream);
        using var pdfDocument = new PdfDocument(pdfReader);

        var pageCount = pdfDocument.GetNumberOfPages();

        for (var pageNum = 1; pageNum <= pageCount; pageNum++)
        {
            var page = pdfDocument.GetPage(pageNum);
            var pageText = PdfTextExtractor.GetTextFromPage(page);

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                textBuilder.AppendLine(pageText);
                textBuilder.AppendLine(); // blank line => a block boundary for the chunker
            }
        }

        return textBuilder.ToString();
    }
}

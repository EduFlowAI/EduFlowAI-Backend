using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace EduFlowAI.AI.Infrastructure.ExternalServices;

public class PdfProcessingService
{
    /// <summary>
    /// Reads a PDF stream, extracts text page-by-page, and splits it into overlapping text chunks.
    /// </summary>
    public List<string> ExtractChunksFromPdf(Stream pdfStream, int chunkSize = 1000, int chunkOverlap = 200)
    {
        if (pdfStream == null || pdfStream.Length == 0)
            throw new ArgumentException("The provided PDF file stream is empty or null.");

        var fullText = ExtractRawText(pdfStream);

        return SliceIntoChunks(fullText, chunkSize, chunkOverlap);
    }

    private static string ExtractRawText(Stream pdfStream)
    {
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
                textBuilder.AppendLine(pageText);
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Splits text into overlapping windows. Pure and side-effect free so it is unit-testable.
    /// Whitespace-only windows are dropped (an embedding call on empty text throws, and the DB
    /// enforces a non-empty Content check), and a bad overlap can never stall the loop.
    /// </summary>
    internal static List<string> SliceIntoChunks(string text, int size = 1000, int overlap = 200)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        if (size <= 0)
            size = 1000;

        // Overlap must be smaller than the window, otherwise the advance step is non-positive.
        if (overlap < 0 || overlap >= size)
            overlap = Math.Min(200, size - 1);

        var normalized = text.Replace("\r\n", " ").Replace("\n", " ");
        var length = normalized.Length;
        var step = size - overlap; // guaranteed > 0

        var start = 0;
        while (start < length)
        {
            var windowLength = Math.Min(size, length - start);
            var chunk = normalized.Substring(start, windowLength).Trim();

            if (chunk.Length > 0)
                chunks.Add(chunk);

            start += step;
        }

        return chunks;
    }
}

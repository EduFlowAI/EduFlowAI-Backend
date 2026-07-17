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

        
        string fullText = ExtractRawText(pdfStream);

        if (string.IsNullOrWhiteSpace(fullText))
            return new List<string>();

        // 2. Slice the extracted full text into overlapping chunks
        return SliceTextIntoChunks(fullText, chunkSize, chunkOverlap);
    }

    private string ExtractRawText(Stream pdfStream)
    {
        var textBuilder = new StringBuilder();

        // Ensure the stream is pointing back to the beginning before reading
        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        using var pdfReader = new PdfReader(pdfStream);
        using var pdfDocument = new PdfDocument(pdfReader);

        int pageCount = pdfDocument.GetNumberOfPages();

        for (int pageNum = 1; pageNum <= pageCount; pageNum++)
        {
            var page = pdfDocument.GetPage(pageNum);
            // Extracts all structural text lines from the specific page layout
            string pageText = PdfTextExtractor.GetTextFromPage(page);

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                textBuilder.AppendLine(pageText);
            }
        }

        return textBuilder.ToString();
    }

    private List<string> SliceTextIntoChunks(string text, int size, int overlap)
    {
        var chunks = new List<string>();

        // Clean up multi-line spaces or carriage returns to make text flow cleanly
        string normalizedText = text.Replace("\r\n", " ").Replace("\n", " ");
        int textLength = normalizedText.Length;
        int startIndex = 0;

        while (startIndex < textLength)
        {
            // Calculate how long this chunk should be
            int currentChunkLength = Math.Min(size, textLength - startIndex);
            string chunkContent = normalizedText.Substring(startIndex, currentChunkLength);

            chunks.Add(chunkContent.Trim());

            // Move the window forward by subtracting the overlap
            startIndex += (size - overlap);

            
            if (size <= overlap)
            {
                startIndex += size;
            }
        }

        return chunks;
    }
}
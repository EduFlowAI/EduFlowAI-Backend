using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using EduFlowAI.AI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Infrastructure.Processing;

/// <summary>
/// Extracts text from an uploaded document, preferring AI transcription.
///
/// Local iText extraction corrupts ligatures ("proficiency" -> "pro?ciency") and interleaves the
/// reading order of multi-column layouts, which poisons the knowledge base at the source. Sending
/// the document to the model instead yields clean Markdown with correct reading order and intact
/// tables. iText remains the fallback for oversized files, transcription failures, or when AI
/// extraction is switched off.
///
/// Plain-text uploads are decoded directly — no model call needed.
/// </summary>
public class DocumentTextExtractor : IDocumentTextExtractor
{
    private const string TranscriptionInstruction = """
Transcribe this document into clean, faithful Markdown for a knowledge base.

Rules:
- Preserve all factual content: headings, lists, tables, numbers, dates, names and links.
- Follow the correct reading order; for multi-column layouts read each column in logical order.
- Keep tabular data as Markdown tables.
- Do not summarise, interpret, add commentary, or invent anything not present in the document.
- Output only the transcription.
""";

    private readonly IGeminiChatClient _chatClient;
    private readonly PdfProcessingService _pdfProcessor;
    private readonly IngestionOptions _options;
    private readonly ILogger<DocumentTextExtractor> _logger;

    public DocumentTextExtractor(
        IGeminiChatClient chatClient,
        PdfProcessingService pdfProcessor,
        IOptions<IngestionOptions> options,
        ILogger<DocumentTextExtractor> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _pdfProcessor = pdfProcessor ?? throw new ArgumentNullException(nameof(pdfProcessor));
        _options = options?.Value ?? new IngestionOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExtractTextAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        if (content == null || content.Length == 0)
            throw new ArgumentException("Document content cannot be empty.", nameof(content));

        // Text uploads need no extraction at all.
        if (IsPlainText(fileName))
            return DecodeText(content);

        if (_options.UseAiExtraction && content.Length <= _options.MaxInlineBytesForAi)
        {
            try
            {
                var transcript = await _chatClient.GenerateFromDocumentAsync(
                    TranscriptionInstruction, content, "application/pdf", cancellationToken);

                if (!string.IsNullOrWhiteSpace(transcript))
                    return transcript;

                _logger.LogWarning("AI transcription returned empty text for {FileName}; falling back to local extraction.", fileName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI transcription failed for {FileName}; falling back to local extraction.", fileName);
            }
        }
        else if (content.Length > _options.MaxInlineBytesForAi)
        {
            _logger.LogInformation(
                "{FileName} is {Bytes} bytes, above the inline limit; using local extraction.", fileName, content.Length);
        }

        using var stream = new MemoryStream(content, writable: false);
        return _pdfProcessor.ExtractRawText(stream);
    }

    public static bool IsPlainText(string fileName) =>
        fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    private static string DecodeText(byte[] content)
    {
        // Honour a UTF-8 BOM if present, otherwise assume UTF-8.
        using var reader = new StreamReader(new MemoryStream(content, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}

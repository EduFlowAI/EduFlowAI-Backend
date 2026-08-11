using System;
using System.Collections.Generic;
using System.Linq;

namespace EduFlowAI.AI.Infrastructure.Processing;

// Chunking can separate a heading from the list underneath it, leaving a chunk that reads
// "- Systems Administration - 2D Animation ..." with nothing saying which branch those belong to. Such a
// chunk is unusable to the model and near-unfindable by embedding. Stamping each chunk with its document
// and section keeps it self-describing.
public static class ChunkContextualizer
{
    private const char LineFeed = '\n';

    public static List<string> Contextualize(
        string? documentName,
        string? sourceText,
        IReadOnlyList<string> chunks)
    {
        if (chunks is null || chunks.Count == 0)
            return new List<string>();

        var normalized = (sourceText ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        var headings = FindHeadings(normalized);
        var contextualized = new List<string>(chunks.Count);
        var searchFrom = 0;

        foreach (var chunk in chunks)
        {
            var position = LocateChunk(normalized, chunk, ref searchFrom);
            var heading = HeadingAt(headings, position);

            contextualized.Add(BuildHeader(documentName, heading) + chunk);
        }

        return contextualized;
    }

    private static string BuildHeader(string? documentName, string? heading)
    {
        var name = string.IsNullOrWhiteSpace(documentName) ? null : documentName.Trim();

        if (name is null && heading is null)
            return string.Empty;

        var parts = new List<string>(2);

        if (name is not null)
            parts.Add($"[Document: {name}]");

        if (heading is not null)
            parts.Add($"[Section: {heading}]");

        return string.Join(" ", parts) + LineFeed;
    }

    // Every Markdown heading line with the offset it starts at, so a chunk can be mapped to the last
    // heading that precedes it.
    private static List<(int Position, string Text)> FindHeadings(string text)
    {
        var headings = new List<(int, string)>();
        var offset = 0;

        foreach (var line in text.Split(LineFeed))
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                var title = trimmed.TrimStart('#').Trim();

                if (title.Length > 0)
                    headings.Add((offset, title));
            }

            offset += line.Length + 1;
        }

        return headings;
    }

    private static string? HeadingAt(List<(int Position, string Text)> headings, int position)
    {
        if (headings.Count == 0 || position < 0)
            return null;

        string? current = null;

        foreach (var heading in headings)
        {
            if (heading.Position > position)
                break;

            current = heading.Text;
        }

        return current;
    }

    // Chunks are contiguous slices of the source, so the opening line locates one. The cursor moves
    // forward so repeated text (overlap, or a list repeated per section) maps to the right occurrence.
    private static int LocateChunk(string text, string chunk, ref int searchFrom)
    {
        var probe = chunk
            .Split(LineFeed)
            .FirstOrDefault(line => line.Trim().Length > 0)
            ?.Trim();

        if (string.IsNullOrEmpty(probe) || text.Length == 0)
            return -1;

        if (probe.Length > 80)
            probe = probe.Substring(0, 80);

        var start = Math.Clamp(searchFrom, 0, Math.Max(0, text.Length - 1));
        var index = text.IndexOf(probe, start, StringComparison.Ordinal);

        if (index < 0)
            index = text.IndexOf(probe, StringComparison.Ordinal);

        if (index >= 0)
            searchFrom = index + 1;

        return index;
    }
}

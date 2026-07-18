using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EduFlowAI.AI.Infrastructure.Processing;

/// <summary>
/// Splits document text into embedding-sized chunks along semantic boundaries.
///
/// The previous implementation cut a blind fixed-width window, which sliced words in half
/// ("d this material.", "sive web, ES.Next"). Fragments like that embed and retrieve poorly.
/// This packs whole paragraphs, falls back to whole sentences, and only ever falls back to whole
/// words — a word is never split (unless a single token is itself longer than the limit).
///
/// Pure and deterministic, so it is fully unit-testable.
/// </summary>
public static class TextChunker
{
    // Sentence end: ., !, ? or the Arabic question mark, followed by whitespace.
    private static readonly Regex SentenceBoundary = new(@"(?<=[\.!\?؟])\s+", RegexOptions.Compiled);

    // One or more blank lines separate blocks (paragraphs, list groups, table bodies).
    private static readonly Regex BlockBoundary = new(@"\n\s*\n", RegexOptions.Compiled);

    public static List<string> Split(string? text, int maxChars = 1200, int overlapChars = 200)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        if (maxChars <= 0)
            maxChars = 1200;

        // Overlap must stay well below the window, otherwise the carried tail leaves no room for
        // the next block and chunks would overflow.
        if (overlapChars < 0)
            overlapChars = 0;
        overlapChars = Math.Min(overlapChars, maxChars / 2);

        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

        var blocks = BlockBoundary.Split(normalized)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0)
            .ToList();

        var current = new StringBuilder();

        foreach (var block in blocks)
        {
            // A block too big to ever fit: flush, then break it down on its own.
            if (block.Length > maxChars)
            {
                Flush(chunks, current);
                foreach (var piece in SplitOversizedBlock(block, maxChars))
                    chunks.Add(piece);
                continue;
            }

            // Adding this block would overflow: close the chunk and carry overlap forward.
            if (current.Length > 0 && current.Length + 1 + block.Length > maxChars)
            {
                var completed = current.ToString().Trim();
                chunks.Add(completed);

                current.Clear();

                // Carry trailing context forward only if the next block still fits beside it,
                // otherwise the overlap itself would push the new chunk over the limit.
                var tail = TakeTail(completed, overlapChars);
                if (tail.Length > 0 && tail.Length + 1 + block.Length <= maxChars)
                    current.Append(tail).Append('\n');
            }

            if (current.Length > 0)
                current.Append('\n');
            current.Append(block);
        }

        Flush(chunks, current);

        return MergeTinyFragments(chunks.Where(c => c.Trim().Length > 0).ToList(), maxChars);
    }

    /// <summary>
    /// Folds very small fragments into the preceding chunk when they fit. Splitting a large
    /// Markdown table can leave a remnant like "23% | |", which carries no retrievable meaning
    /// yet still costs an embedding. Merging keeps the content without the useless vector.
    /// </summary>
    private static List<string> MergeTinyFragments(List<string> chunks, int maxChars)
    {
        const int minUsefulChars = 40;

        var merged = new List<string>();

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];

            if (chunk.Trim().Length >= minUsefulChars)
            {
                merged.Add(chunk);
                continue;
            }

            // Prefer folding backwards so the fragment stays after the text it came from.
            if (merged.Count > 0 && merged[^1].Length + 1 + chunk.Length <= maxChars)
            {
                merged[^1] = merged[^1] + "\n" + chunk;
                continue;
            }

            // The previous chunk is full (common right after a large table). Fold forwards instead.
            if (i + 1 < chunks.Count && chunks[i + 1].Length + 1 + chunk.Length <= maxChars)
            {
                chunks[i + 1] = chunk + "\n" + chunks[i + 1];
                continue;
            }

            merged.Add(chunk); // nowhere it fits; keep it rather than lose content
        }

        return merged;
    }

    private static void Flush(List<string> chunks, StringBuilder buffer)
    {
        if (buffer.Length == 0)
            return;

        var value = buffer.ToString().Trim();
        if (value.Length > 0)
            chunks.Add(value);

        buffer.Clear();
    }

    /// <summary>Breaks an oversized block on sentence boundaries, then on word boundaries.</summary>
    private static IEnumerable<string> SplitOversizedBlock(string block, int maxChars)
    {
        var buffer = new StringBuilder();

        foreach (var sentence in SentenceBoundary.Split(block).Where(s => s.Trim().Length > 0))
        {
            var trimmed = sentence.Trim();

            if (trimmed.Length > maxChars)
            {
                if (buffer.Length > 0)
                {
                    yield return buffer.ToString().Trim();
                    buffer.Clear();
                }

                foreach (var wordChunk in SplitOnWords(trimmed, maxChars))
                    yield return wordChunk;

                continue;
            }

            if (buffer.Length > 0 && buffer.Length + 1 + trimmed.Length > maxChars)
            {
                yield return buffer.ToString().Trim();
                buffer.Clear();
            }

            if (buffer.Length > 0)
                buffer.Append(' ');
            buffer.Append(trimmed);
        }

        if (buffer.Length > 0)
            yield return buffer.ToString().Trim();
    }

    /// <summary>Last resort: pack whole words. Only a single token longer than the limit is cut.</summary>
    private static IEnumerable<string> SplitOnWords(string sentence, int maxChars)
    {
        var buffer = new StringBuilder();

        foreach (var word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length > maxChars)
            {
                if (buffer.Length > 0)
                {
                    yield return buffer.ToString().Trim();
                    buffer.Clear();
                }

                for (var i = 0; i < word.Length; i += maxChars)
                    yield return word.Substring(i, Math.Min(maxChars, word.Length - i));

                continue;
            }

            if (buffer.Length > 0 && buffer.Length + 1 + word.Length > maxChars)
            {
                yield return buffer.ToString().Trim();
                buffer.Clear();
            }

            if (buffer.Length > 0)
                buffer.Append(' ');
            buffer.Append(word);
        }

        if (buffer.Length > 0)
            yield return buffer.ToString().Trim();
    }

    /// <summary>Trailing context for the next chunk, always starting at a word boundary.</summary>
    private static string TakeTail(string text, int overlapChars)
    {
        if (overlapChars <= 0)
            return string.Empty;

        if (text.Length <= overlapChars)
            return text.Trim();

        var tail = text.Substring(text.Length - overlapChars);

        // Drop a partial leading word so the overlap never begins mid-token.
        var firstSpace = tail.IndexOfAny(new[] { ' ', '\n' });
        if (firstSpace >= 0)
            tail = tail.Substring(firstSpace + 1);

        return tail.Trim();
    }
}

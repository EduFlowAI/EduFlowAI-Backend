using System.Text.RegularExpressions;

namespace EduFlowAI.AI.Application.Services;

// The chat renders replies as plain text, so Markdown from the model reaches the applicant as literal
// asterisks and hashes. Prompts ask for plain text; this strips whatever still slips through.
public static partial class PlainTextFormatter
{
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleaned = CodeFence().Replace(text, string.Empty);
        cleaned = cleaned.Replace("`", string.Empty);

        cleaned = BoldItalic().Replace(cleaned, "$1");
        cleaned = Bold().Replace(cleaned, "$1");
        cleaned = Underscored().Replace(cleaned, "$1");
        cleaned = Emphasis().Replace(cleaned, "$1");

        cleaned = Heading().Replace(cleaned, string.Empty);
        cleaned = Bullet().Replace(cleaned, "• ");
        cleaned = TrailingSpace().Replace(cleaned, string.Empty);
        cleaned = ExtraBlankLines().Replace(cleaned, "\n\n");

        return cleaned.Trim();
    }

    [GeneratedRegex(@"^\s*```.*$", RegexOptions.Multiline)]
    private static partial Regex CodeFence();

    [GeneratedRegex(@"\*\*\*(.+?)\*\*\*", RegexOptions.Singleline)]
    private static partial Regex BoldItalic();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)]
    private static partial Regex Bold();

    [GeneratedRegex(@"__(.+?)__", RegexOptions.Singleline)]
    private static partial Regex Underscored();

    // Only a full *emphasis* span, so a lone asterisk or a snake_case word is left alone.
    [GeneratedRegex(@"(?<![\w*])\*(?!\s)([^*\n]+?)(?<!\s)\*(?![\w*])")]
    private static partial Regex Emphasis();

    [GeneratedRegex(@"^[ \t]{0,3}#{1,6}[ \t]*", RegexOptions.Multiline)]
    private static partial Regex Heading();

    // Markdown bullets become a real bullet character, which reads correctly as plain text.
    [GeneratedRegex(@"^[ \t]*[-*+•][ \t]+", RegexOptions.Multiline)]
    private static partial Regex Bullet();

    [GeneratedRegex(@"[ \t]+$", RegexOptions.Multiline)]
    private static partial Regex TrailingSpace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLines();
}

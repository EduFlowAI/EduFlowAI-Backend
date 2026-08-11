using EduFlowAI.AI.Application.Services;

namespace EduFlowAI.AI.Tests;

// The chat renders replies as plain text, so Markdown reached the applicant as literal asterisks.
public class PlainTextFormatterTests
{
    [Fact]
    public void Bold_markers_are_removed_but_the_words_stay()
    {
        var result = PlainTextFormatter.Clean("We offer **AI and Machine Learning** this intake.");

        Assert.Equal("We offer AI and Machine Learning this intake.", result);
    }

    [Fact]
    public void Markdown_bullets_become_a_real_bullet_character()
    {
        var raw = "We offer 2 tracks:\n*   **AI and Machine Learning** (Cognitive)\n-  Systems Administration";

        var result = PlainTextFormatter.Clean(raw);

        Assert.DoesNotContain("*", result);
        Assert.Contains("• AI and Machine Learning (Cognitive)", result);
        Assert.Contains("• Systems Administration", result);
    }

    // The exact shape the assistant produced for "which tracks are offered at Alexandria".
    [Fact]
    public void An_arabic_reply_keeps_its_words_and_loses_its_asterisks()
    {
        var raw = "نقدم في فرع الإسكندرية 6 مسارات:\n*   **AI and Machine Learning** (الذكاء الاصطناعي)";

        var result = PlainTextFormatter.Clean(raw);

        Assert.DoesNotContain("*", result);
        Assert.Contains("نقدم في فرع الإسكندرية 6 مسارات:", result);
        Assert.Contains("• AI and Machine Learning (الذكاء الاصطناعي)", result);
    }

    [Fact]
    public void Headings_backticks_and_italics_are_stripped()
    {
        var result = PlainTextFormatter.Clean("## Requirements\nA `Bachelor` degree is *required*.");

        Assert.Equal("Requirements\nA Bachelor degree is required.", result);
    }

    [Fact]
    public void Runs_of_blank_lines_collapse_to_one()
    {
        var result = PlainTextFormatter.Clean("First paragraph.\n\n\n\nSecond paragraph.");

        Assert.Equal("First paragraph.\n\nSecond paragraph.", result);
    }

    // A lone asterisk or an identifier is not emphasis and must survive untouched.
    [Theory]
    [InlineData("Rated 4.5 * out of 5", "Rated 4.5 * out of 5")]
    [InlineData("The field is called user_name here", "The field is called user_name here")]
    public void Text_that_only_looks_like_markdown_is_left_alone(string raw, string expected)
    {
        Assert.Equal(expected, PlainTextFormatter.Clean(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_to_clean_gives_an_empty_string(string? raw)
    {
        Assert.Equal(string.Empty, PlainTextFormatter.Clean(raw));
    }
}

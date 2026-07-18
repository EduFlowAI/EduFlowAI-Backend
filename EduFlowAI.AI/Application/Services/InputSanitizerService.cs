using System.Text.RegularExpressions;

namespace EduFlowAI.AI.Application.Services;

/// <summary>
/// Light defensive scrub applied before user text is sent to Gemini. Applicants are not expected
/// to type their National ID into the chat, so this is a safety net rather than a full PII engine:
/// any run of 12 or more digits (an Egyptian National ID is 14) is masked. \d also covers
/// Arabic-Indic digits, so ٠-٩ sequences are masked too.
/// </summary>
public static class InputSanitizerService
{
    public const string Mask = "[hidden]";

    private static readonly Regex LongDigitRun = new(@"\d{12,}", RegexOptions.Compiled);

    public static string MaskSensitiveNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        return LongDigitRun.Replace(text, Mask);
    }
}

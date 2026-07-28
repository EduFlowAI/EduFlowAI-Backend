using EduFlowAI.AI.Presentation.DTOs;

namespace EduFlowAI.AI.Presentation.Validators;

public static class AssistantMessageRequestValidator
{
    private const int MaximumMessageLength = 2000;

    private static readonly string[] SupportedLanguages =
    [
        "en",
        "ar"
    ];

    public static IReadOnlyList<string> Validate(
        AssistantMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            errors.Add($"{nameof(request.Message)} is required.");
        }
        else if (request.Message.Length > MaximumMessageLength)
        {
            errors.Add(
                $"{nameof(request.Message)} must not exceed " +
                $"{MaximumMessageLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Language) &&
            !SupportedLanguages.Contains(
                request.Language,
                StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{nameof(request.Language)} must be either 'en' or 'ar'.");
        }

        return errors;
    }
}
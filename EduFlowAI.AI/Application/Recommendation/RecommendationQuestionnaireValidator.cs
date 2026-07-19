using EduFlowAI.AI.Application.Recommendation.Models;

namespace EduFlowAI.AI.Application.Recommendation;

public static class RecommendationQuestionnaireValidator
{
    private const int MaximumItemsPerField = 20;
    private const int MaximumTextLength = 500;

    public static IReadOnlyList<string> Validate(
        RecommendationQuestionnaire questionnaire)
    {
        ArgumentNullException.ThrowIfNull(questionnaire);

        var errors = new List<string>();

        ValidateRequiredText(
            questionnaire.Major,
            nameof(questionnaire.Major),
            errors);

        ValidateCollection(
            questionnaire.TechnicalCourses,
            nameof(questionnaire.TechnicalCourses),
            errors);

        ValidateCollection(
            questionnaire.Skills,
            nameof(questionnaire.Skills),
            errors);

        ValidateCollection(
            questionnaire.Interests,
            nameof(questionnaire.Interests),
            errors);

        ValidateCollection(
            questionnaire.PreferredActivities,
            nameof(questionnaire.PreferredActivities),
            errors);

        ValidateCollection(
            questionnaire.CareerGoals,
            nameof(questionnaire.CareerGoals),
            errors);

        if (questionnaire.AdditionalContext?.Length > MaximumTextLength)
        {
            errors.Add(
                $"{nameof(questionnaire.AdditionalContext)} must not exceed " +
                $"{MaximumTextLength} characters.");
        }

        return errors;
    }

    private static void ValidateRequiredText(
        string? value,
        string fieldName,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (value.Length > MaximumTextLength)
        {
            errors.Add(
                $"{fieldName} must not exceed {MaximumTextLength} characters.");
        }
    }

    private static void ValidateCollection(
        IReadOnlyCollection<string>? values,
        string fieldName,
        ICollection<string> errors)
    {
        if (values is null || values.Count == 0)
        {
            errors.Add($"{fieldName} must contain at least one value.");
            return;
        }

        if (values.Count > MaximumItemsPerField)
        {
            errors.Add(
                $"{fieldName} must not contain more than " +
                $"{MaximumItemsPerField} values.");
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"{fieldName} must not contain empty values.");
        }

        if (values.Any(value => value.Length > MaximumTextLength))
        {
            errors.Add(
                $"Each value in {fieldName} must not exceed " +
                $"{MaximumTextLength} characters.");
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DocumentVerification;
using EduFlowAI.AI.Infrastructure.DocumentVerification;
using EduFlowAI.AI.Infrastructure.DocumentVerification.Gemini;
using EduFlowAI.Shared.Messaging.Contracts.Documents.V1;

namespace EduFlowAI.AI.Tests;

// A graduation certificate usually prints the name in Arabic only, and a scan rarely reproduces
// diacritics or hamza forms exactly. Neither should send an otherwise valid document to a reviewer.
public sealed class DocumentFieldToleranceTests
{
    private static readonly Dictionary<string, string> GraduationFields = new()
    {
        ["FullNameEn"] = "Ahmed Mohamed Ali",
        ["FullNameAr"] = "أحمد محمد علي",
        ["University"] = "Cairo University",
        ["Faculty"] = "Engineering",
        ["Major"] = "Computer Science",
        ["GraduationYear"] = "2023"
    };

    [Fact]
    public async Task An_arabic_only_certificate_is_approved_and_the_english_name_is_not_held_against_it()
    {
        const string json = """
            {
              "outcome": "MissingRequiredData",
              "fields": [
                { "fieldName": "FullNameEn", "extractedValue": null, "isMatch": false },
                { "fieldName": "FullNameAr", "extractedValue": "أحمد محمد علي", "isMatch": true },
                { "fieldName": "University", "extractedValue": "Cairo University", "isMatch": true },
                { "fieldName": "Faculty", "extractedValue": "Engineering", "isMatch": true },
                { "fieldName": "Major", "extractedValue": "Computer Science", "isMatch": true },
                { "fieldName": "GraduationYear", "extractedValue": "2023", "isMatch": true }
              ],
              "warnings": []
            }
            """;

        var result = await VerifyAsync(json);

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
        Assert.Empty(result.Details.MissingFields);
        Assert.DoesNotContain(result.Details.Fields, f => f.FieldName == "FullNameEn");
        Assert.Contains(result.Details.Warnings, w => w.Contains("FullNameEn"));
    }

    [Fact]
    public async Task An_english_only_certificate_is_still_approved()
    {
        const string json = """
            {
              "outcome": "MissingRequiredData",
              "fields": [
                { "fieldName": "FullNameEn", "extractedValue": "Ahmed Mohamed Ali", "isMatch": true },
                { "fieldName": "FullNameAr", "extractedValue": null, "isMatch": false },
                { "fieldName": "University", "extractedValue": "Cairo University", "isMatch": true },
                { "fieldName": "Faculty", "extractedValue": "Engineering", "isMatch": true },
                { "fieldName": "Major", "extractedValue": "Computer Science", "isMatch": true },
                { "fieldName": "GraduationYear", "extractedValue": "2023", "isMatch": true }
              ],
              "warnings": []
            }
            """;

        var result = await VerifyAsync(json);

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
        Assert.DoesNotContain(result.Details.Fields, f => f.FieldName == "FullNameAr");
    }

    [Fact]
    public async Task Neither_name_matching_still_goes_to_a_reviewer()
    {
        const string json = """
            {
              "outcome": "ValidButDifferent",
              "fields": [
                { "fieldName": "FullNameEn", "extractedValue": "Mahmoud Sayed", "isMatch": false },
                { "fieldName": "FullNameAr", "extractedValue": "محمود سيد", "isMatch": false },
                { "fieldName": "University", "extractedValue": "Cairo University", "isMatch": true },
                { "fieldName": "Faculty", "extractedValue": "Engineering", "isMatch": true },
                { "fieldName": "Major", "extractedValue": "Computer Science", "isMatch": true },
                { "fieldName": "GraduationYear", "extractedValue": "2023", "isMatch": true }
              ],
              "warnings": []
            }
            """;

        var result = await VerifyAsync(json);

        Assert.Equal(DocumentVerificationOutcomeV1.ValidButDifferent, result.Outcome);
        Assert.Contains(result.Details.Fields, f => f.FieldName == "FullNameEn");
        Assert.Contains(result.Details.Fields, f => f.FieldName == "FullNameAr");
    }

    // Diacritics, hamza and alef forms, tatweel, punctuation and doubled spaces.
    [Theory]
    [InlineData("أحمد محمد علي", "احمد محمد على")]
    [InlineData("أحمد محمد علي", "أَحْمَد مُحَمَّد عَلِي")]
    [InlineData("أحمد محمد علي", "أحـمـد محمد علي")]
    [InlineData("Ahmed Mohamed Ali", "ahmed  mohamed, ali")]
    [InlineData("Cairo University", "cairo university.")]
    public async Task Cosmetic_differences_are_treated_as_a_match(string expected, string extracted)
    {
        var fields = new Dictionary<string, string>(GraduationFields);
        var fieldName = expected.Any(char.IsLetter) && expected[0] > 0x600 ? "FullNameAr" : "FullNameEn";
        fields[fieldName] = expected;

        var json = $$"""
            {
              "outcome": "ValidButDifferent",
              "fields": [
                { "fieldName": "{{fieldName}}", "extractedValue": "{{extracted}}", "isMatch": false },
                { "fieldName": "{{(fieldName == "FullNameAr" ? "FullNameEn" : "FullNameAr")}}", "extractedValue": null, "isMatch": false },
                { "fieldName": "University", "extractedValue": "Cairo University", "isMatch": true },
                { "fieldName": "Faculty", "extractedValue": "Engineering", "isMatch": true },
                { "fieldName": "Major", "extractedValue": "Computer Science", "isMatch": true },
                { "fieldName": "GraduationYear", "extractedValue": "2023", "isMatch": true }
              ],
              "warnings": []
            }
            """;

        var result = await VerifyAsync(json, fields);

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
    }

    // Identifiers and dates get no leeway - one different digit is a different person or year.
    [Theory]
    [InlineData("GraduationYear", "2023", "2024")]
    [InlineData("NationalId", "29001011234567", "29001011234568")]
    [InlineData("DateOfBirth", "1998-04-12", "1998-04-13")]
    public async Task A_single_digit_difference_in_an_identifier_is_never_neglected(
        string fieldName, string expected, string extracted)
    {
        var fields = new Dictionary<string, string> { [fieldName] = expected };

        var json = $$"""
            {
              "outcome": "ValidButDifferent",
              "fields": [
                { "fieldName": "{{fieldName}}", "extractedValue": "{{extracted}}", "isMatch": false }
              ],
              "warnings": []
            }
            """;

        var result = await VerifyAsync(json, fields);

        Assert.Equal(DocumentVerificationOutcomeV1.ValidButDifferent, result.Outcome);
    }

    [Fact]
    public async Task An_unreadable_document_is_never_rescued_by_tolerance()
    {
        const string json = """
            {
              "outcome": "UnreadableDocument",
              "fields": [
                { "fieldName": "FullNameEn", "extractedValue": null, "isMatch": false },
                { "fieldName": "FullNameAr", "extractedValue": null, "isMatch": false },
                { "fieldName": "University", "extractedValue": null, "isMatch": false },
                { "fieldName": "Faculty", "extractedValue": null, "isMatch": false },
                { "fieldName": "Major", "extractedValue": null, "isMatch": false },
                { "fieldName": "GraduationYear", "extractedValue": null, "isMatch": false }
              ],
              "warnings": []
            }
            """;

        var result = await VerifyAsync(json);

        Assert.Equal(DocumentVerificationOutcomeV1.UnreadableDocument, result.Outcome);
    }

    private static async Task<DocumentVerificationResult> VerifyAsync(
        string responseJson,
        IReadOnlyDictionary<string, string>? expectedFields = null)
    {
        var service = new DocumentVerificationService(new FakeClient(responseJson));
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        return await service.VerifyAsync(
            new DocumentVerificationInput(
                DocumentId: Guid.NewGuid(),
                DocumentType: "GraduationCertificate",
                OriginalFileName: "certificate.pdf",
                File: stream,
                ExpectedFields: expectedFields ?? GraduationFields),
            CancellationToken.None);
    }

    private sealed class FakeClient : IDocumentVerificationGeminiClient
    {
        private readonly string _response;

        public FakeClient(string response) => _response = response;

        public Task<DocumentVerificationGeminiResult> GenerateVerificationJsonAsync(
            string documentType,
            IReadOnlyDictionary<string, string> expectedFields,
            byte[] fileContent,
            string mimeType,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentVerificationGeminiResult(_response, "gemini-3.1-flash-lite"));
    }
}

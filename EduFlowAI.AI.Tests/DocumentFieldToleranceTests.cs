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

    // ---------------------------------------------------------------- real documents
    //
    // Values below are taken from the sample national ID card and birth certificate. The child holds
    // 30304080200955 and the father 26501140200317, and the father's full name is a clean suffix of
    // the child's - which is exactly what must never verify.

    private const string ChildFullName = "عبدالرحمن السيد احمد كمال السيد سليم";
    private const string ChildNationalId = "30304080200955";
    private const string FatherFullName = "السيد احمد كمال السيد سليم";
    private const string FatherNationalId = "26501140200317";

    private static Dictionary<string, string> BirthCertificateFields() => new()
    {
        ["FullNameAr"] = ChildFullName,
        ["DateOfBirth"] = "2003-04-08",
        ["NationalId"] = ChildNationalId
    };

    // The certificate records the child under بيانات المولود by first name only.
    [Fact]
    public async Task A_birth_certificate_naming_only_the_child_first_name_is_approved()
    {
        var json = Response("ExactMatch",
            Field("FullNameAr", "عبد الرحمن", true),
            Field("DateOfBirth", "2003-04-08", true),
            Field("NationalId", ChildNationalId, true));

        var result = await VerifyAsync(json, BirthCertificateFields(), "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
    }

    [Fact]
    public async Task The_fathers_name_never_verifies_as_the_applicant()
    {
        var json = Response("ValidButDifferent",
            Field("FullNameAr", FatherFullName, false),
            Field("DateOfBirth", "2003-04-08", true),
            Field("NationalId", ChildNationalId, true));

        var result = await VerifyAsync(json, BirthCertificateFields(), "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ValidButDifferent, result.Outcome);
    }

    [Fact]
    public async Task The_fathers_national_number_never_verifies_as_the_applicant()
    {
        var json = Response("ValidButDifferent",
            Field("FullNameAr", ChildFullName, true),
            Field("DateOfBirth", "2003-04-08", true),
            Field("NationalId", FatherNationalId, false));

        var result = await VerifyAsync(json, BirthCertificateFields(), "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ValidButDifferent, result.Outcome);
    }

    // A missing middle name, and the sample address trailing the name.
    [Theory]
    [InlineData("عبدالرحمن السيد سليم")]
    [InlineData("عبد الرحمن السيد احمد كمال السيد سليم")]
    [InlineData("عبدالرحمن السيد احمد كمال السيد سليم ابويوسف خلف هالة السياحى هانوفيل الدخيله الاسكندرية")]
    public async Task A_name_the_document_writes_differently_still_matches(string extracted)
    {
        var json = Response("ValidButDifferent",
            Field("FullNameAr", extracted, false),
            Field("DateOfBirth", "2003-04-08", true),
            Field("NationalId", ChildNationalId, true));

        var result = await VerifyAsync(json, BirthCertificateFields(), "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
    }

    [Fact]
    public async Task A_relative_sharing_part_of_the_name_is_not_the_applicant()
    {
        var fields = new Dictionary<string, string> { ["FullNameAr"] = "محمد احمد علي حسن" };
        var json = Response("ValidButDifferent", Field("FullNameAr", "احمد علي", false));

        var result = await VerifyAsync(json, fields, "NationalId");

        Assert.Equal(DocumentVerificationOutcomeV1.ValidButDifferent, result.Outcome);
    }

    // The card prints the date beside an issue and an expiry date, so the model can grab the wrong one.
    [Fact]
    public async Task A_misread_birth_date_is_confirmed_from_the_national_number()
    {
        var json = Response("ValidButDifferent",
            Field("FullNameAr", ChildFullName, true),
            Field("DateOfBirth", "2027-10-25", false),
            Field("NationalId", ChildNationalId, true));

        var result = await VerifyAsync(json, BirthCertificateFields(), "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
        Assert.Equal("2003-04-08",
            result.Details.Fields.Single(f => f.FieldName == "DateOfBirth").ExtractedValue);
        Assert.Contains(result.Details.Warnings, w => w.Contains("national number"));
    }

    [Fact]
    public async Task A_national_number_implying_a_different_birth_date_is_still_a_mismatch()
    {
        var fields = new Dictionary<string, string>
        {
            ["FullNameAr"] = ChildFullName,
            ["DateOfBirth"] = "1999-01-01",
            ["NationalId"] = ChildNationalId
        };

        var json = Response("ValidButDifferent",
            Field("FullNameAr", ChildFullName, true),
            Field("DateOfBirth", "2003-04-08", false),
            Field("NationalId", ChildNationalId, true));

        var result = await VerifyAsync(json, fields, "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ValidButDifferent, result.Outcome);
    }

    // Older certificates do not print it, and that absence must not fail the document.
    [Fact]
    public async Task A_birth_certificate_without_a_national_number_is_not_failed_for_it()
    {
        var json = Response("MissingRequiredData",
            Field("FullNameAr", ChildFullName, true),
            Field("DateOfBirth", "2003-04-08", true),
            Field("NationalId", null, false));

        var result = await VerifyAsync(json, BirthCertificateFields(), "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
        Assert.DoesNotContain(result.Details.Fields, f => f.FieldName == "NationalId");
        Assert.Contains(result.Details.Warnings, w => w.Contains("not printed"));
    }

    // The card spaces the digits out and prints them in Arabic-Indic form.
    [Fact]
    public async Task The_national_number_matches_however_the_card_prints_it()
    {
        var json = Response("ValidButDifferent",
            Field("FullNameAr", ChildFullName, true),
            Field("DateOfBirth", "2003-04-08", true),
            Field("NationalId", "٣٠٣ ٠٤ ٠٨ ٠٢ ٠٠٩ ٥٥", false));

        var result = await VerifyAsync(json, BirthCertificateFields(), "BirthCertificate");

        Assert.Equal(DocumentVerificationOutcomeV1.ExactMatch, result.Outcome);
    }

    [Theory]
    [InlineData("جنوب الوادي", "جامعة جنوب الوادى", true)]
    [InlineData("Cairo University", "cairo  university", true)]
    [InlineData("جامعة القاهرة", "جامعة جنوب الوادي", false)]
    public async Task A_university_named_more_fully_by_the_document_still_matches(
        string expected, string extracted, bool shouldMatch)
    {
        var fields = new Dictionary<string, string>(GraduationFields) { ["University"] = expected };

        var json = Response("ValidButDifferent",
            Field("FullNameEn", "Ahmed Mohamed Ali", true),
            Field("FullNameAr", null, false),
            Field("University", extracted, false),
            Field("Faculty", "Engineering", true),
            Field("Major", "Computer Science", true),
            Field("GraduationYear", "2023", true));

        var result = await VerifyAsync(json, fields);

        Assert.Equal(
            shouldMatch
                ? DocumentVerificationOutcomeV1.ExactMatch
                : DocumentVerificationOutcomeV1.ValidButDifferent,
            result.Outcome);
    }

    // ---------------------------------------------------------------- helpers

    private static string Field(string name, string? extracted, bool isMatch) =>
        $$"""{ "fieldName": "{{name}}", "extractedValue": {{(extracted is null ? "null" : $"\"{extracted}\"")}}, "isMatch": {{(isMatch ? "true" : "false")}} }""";

    private static string Response(string outcome, params string[] fields) =>
        $$"""
          { "outcome": "{{outcome}}", "fields": [ {{string.Join(", ", fields)}} ], "warnings": [] }
          """;

    private static async Task<DocumentVerificationResult> VerifyAsync(
        string responseJson,
        IReadOnlyDictionary<string, string>? expectedFields = null,
        string documentType = "GraduationCertificate")
    {
        var service = new DocumentVerificationService(new FakeClient(responseJson));
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        return await service.VerifyAsync(
            new DocumentVerificationInput(
                DocumentId: Guid.NewGuid(),
                DocumentType: documentType,
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

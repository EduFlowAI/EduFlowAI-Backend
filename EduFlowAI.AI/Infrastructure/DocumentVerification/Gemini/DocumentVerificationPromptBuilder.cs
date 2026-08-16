using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace EduFlowAI.AI.Infrastructure.DocumentVerification.Gemini;

internal static class DocumentVerificationPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string BuildInstruction(
        string documentType,
        IReadOnlyDictionary<string, string> expectedFields)
    {
        var expected = expectedFields.Select(pair => new { field = pair.Key, expectedValue = pair.Value });

        return $$"""
            You are a document verification assistant for a university admission platform.

            The attached file is claimed to be a "{{documentType}}" belonging to an applicant.

            Task:
            - Read the attached document and extract the value of each expected field listed below.
            - Compare each extracted value against the expected value supplied for that field.
            - Decide isMatch for each field: true only if the extracted value clearly matches the
              expected value. Ignore cosmetic differences - spacing, letter case, punctuation, date
              format, Arabic diacritics, tatweel, and the interchangeable forms of alef, hamza,
              taa marbuta and alef maqsura. A genuinely different name, number or year is not a match.

            Whose data to read:
            - A document often names several people. Only the subject's data counts: the card holder,
              or the child under "بيانات المولود" on a birth certificate.
            - Never take a name or a national number from a parent or guardian section such as
              "بيانات الأب" or "بيانات الأم", even when it is the only full name on the page.

            Reading a name:
            - Extract only the person's own name. It often wraps across two or more lines - join them.
            - Stop where the name stops. Never include the address, street, building, district, city
              or governorate, and never include profession, religion, marital status, titles, dates
              or numbers.

            Comparing meaning:
            - The expected values come from a form the applicant filled in, so they may be written in
              a different language from the document. Egyptian documents are usually in Arabic and
              applicants often type English.
            - For University, Faculty, Major and MilitaryStatus, compare the MEANING, not the
              characters. The Arabic name of the same faculty the applicant typed in English is a
              match: "الحاسبات والمعلومات" is "Computers and Information", "علوم الحاسب" is
              "Computer Science", and "جامعة جنوب الوادي" is "South Valley University".
            - Ignore qualifier words a document adds and a form leaves out, such as "جامعة", "كلية",
              "قسم", "University", "Faculty of" and "Department of".
            - A genuinely different institution, faculty, person, number or year is never a match.

            How to write extractedValue:
            - Write every date as yyyy-MM-dd, whatever form the document uses. Dates may be spelled
              out in words: "ثمانية من إبريل عام ألفين وثلاثة" is 2003-04-08.
            - Write every number using ASCII digits with no spaces or separators, so
              "٣٠٣٠٤٠٨٠٢٠٠٩٥٥" becomes 30304080200955.

            Rules:
            - Return exactly one result per expected field below - no more, no fewer, no duplicates,
              no extra fields.
            - If the document cannot be read at all, set outcome to "UnreadableDocument".
            - If the document is clearly not a {{documentType}}, set outcome to "InvalidDocumentType".
            - If one or more expected fields cannot be found in the document, set outcome to
              "MissingRequiredData" and still return a result row for every expected field (leave
              extractedValue null and isMatch false for the ones you could not find).
            - If every expected field was found and every one matches, set outcome to "ExactMatch".
            - If every expected field was found but at least one does not match, set outcome to
              "ValidButDifferent".
            - A name may be printed in Arabic or in English, and often in one script only. When both
              FullNameEn and FullNameAr are listed, report each separately: extract whichever form
              appears and leave the other's extractedValue null. Never translate or transliterate one
              form into the other to force a match.
            - Never guess or invent a value. Only report what is actually visible in the document.
            - Return JSON matching the requested schema exactly. Do not include any commentary.

            Expected fields:
            {{JsonSerializer.Serialize(expected, JsonOptions)}}
            """;
    }

    public static object BuildResponseSchema() => new
    {
        type = "object",
        properties = new
        {
            outcome = new
            {
                type = "string",
                @enum = new[]
                {
                    "ExactMatch",
                    "ValidButDifferent",
                    "MissingRequiredData",
                    "UnreadableDocument",
                    "InvalidDocumentType"
                }
            },
            fields = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        fieldName = new { type = "string" },
                        extractedValue = new { type = "string", nullable = true },
                        isMatch = new { type = "boolean" },
                        notes = new { type = "string", nullable = true }
                    },
                    required = new[] { "fieldName", "isMatch" }
                }
            },
            warnings = new
            {
                type = "array",
                items = new { type = "string" }
            },
            confidenceScore = new
            {
                type = "number",
                nullable = true
            }
        },
        required = new[] { "outcome", "fields" }
    };
}

using EduFlowAI.AI.Application.DocumentVerification;
using EduFlowAI.AI.Infrastructure.DocumentVerification;
using EduFlowAI.AI.Infrastructure.DocumentVerification.Gemini;
using EduFlowAI.AI.Infrastructure.Options;
using Microsoft.Extensions.Options;

var geminiOptions = Options.Create(new GeminiOptions
{
    ApiKey = "AQ.Ab8RN6I1GRGXGvMghVuDmCNfxh9jm1D0vPAp-ot6AOm3ZaY79w",
    ChatModel = "gemini-3.5-flash",
    BaseUrl = "https://generativelanguage.googleapis.com/v1beta/"
});

using var httpClient = new HttpClient { BaseAddress = new Uri(geminiOptions.Value.BaseUrl) };
var geminiClient = new DocumentVerificationGeminiClient(httpClient, geminiOptions);
var service = new DocumentVerificationService(geminiClient);

await using var file = File.OpenRead(@"C:\Users\seifa\Downloads\nationalid.jpeg");

var result = await service.VerifyAsync(
    new DocumentVerificationInput(
        DocumentId: Guid.NewGuid(),
        DocumentType: "NationalId",
        OriginalFileName: "sample-national-id.jpg",
        File: file,
        ExpectedFields: new Dictionary<string, string>
        {
            ["FullNameAr"] = "سيف الدين هاني شعبان عوض جابر",
            ["NationalId"] = "30312180201773"
        }),
    CancellationToken.None);

Console.WriteLine($"Outcome: {result.Outcome}");
foreach (var f in result.Details.Fields)
{
    Console.WriteLine($"  {f.FieldName}: extracted='{f.ExtractedValue}' expected='{f.ExpectedValue}' match={f.IsMatch}");
}
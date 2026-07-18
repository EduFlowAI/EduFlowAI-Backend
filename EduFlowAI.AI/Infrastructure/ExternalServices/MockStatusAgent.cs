using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Infrastructure.ExternalServices;

/// <summary>
/// Placeholder Status Agent. Deterministic (no randomness) and language-aware so demos are stable
/// and Arabic questions get an Arabic reply. Replaced by the Applications module's real service.
/// </summary>
public class MockStatusAgent : IStatusAgent
{
    private const string ArabicResponse =
        "طلبك قيد المراجعة حالياً وسيتم إعلامك فور صدور القرار. (هذه استجابة تجريبية مؤقتة لخدمة حالة الطلب.)";

    private const string EnglishResponse =
        "Your application is currently under review. You will be notified as soon as a decision is made. (This is a temporary placeholder status response.)";

    public Task<string> GetResponseAsync(string question, string language, CancellationToken cancellationToken = default)
    {
        var response = language == "ar" ? ArabicResponse : EnglishResponse;
        return Task.FromResult(response);
    }
}

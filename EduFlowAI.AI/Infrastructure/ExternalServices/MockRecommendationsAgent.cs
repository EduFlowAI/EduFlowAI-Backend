using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Infrastructure.ExternalServices;

public class MockRecommendationsAgent : IRecommendationsAgent
{
    private const string ArabicResponse =
        "بناءً على اهتماماتك، ننصح باستكشاف مسارات علوم الحاسب أو الذكاء الاصطناعي. (هذه استجابة تجريبية مؤقتة لخدمة التوصيات.)";

    private const string EnglishResponse =
        "Based on your interests, we suggest exploring the Computer Science or Artificial Intelligence tracks. (This is a temporary placeholder recommendation response.)";

    public Task<string> GetResponseAsync(string question, string language, CancellationToken cancellationToken = default)
    {
        var response = language == "ar" ? ArabicResponse : EnglishResponse;
        return Task.FromResult(response);
    }
}

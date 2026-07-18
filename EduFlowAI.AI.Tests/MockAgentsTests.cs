using System.Threading.Tasks;
using EduFlowAI.AI.Infrastructure.ExternalServices;

namespace EduFlowAI.AI.Tests;

public class MockAgentsTests
{
    [Fact]
    public async Task StatusAgent_ArabicQuestion_RepliesInArabic()
    {
        var agent = new MockStatusAgent();

        var answer = await agent.GetResponseAsync("ما حالة طلبي؟", "ar");

        Assert.Contains("طلبك", answer);
    }

    [Fact]
    public async Task StatusAgent_EnglishQuestion_RepliesInEnglish()
    {
        var agent = new MockStatusAgent();

        var answer = await agent.GetResponseAsync("What is my status?", "en");

        Assert.Contains("under review", answer);
    }

    [Fact]
    public async Task RecommendationsAgent_ArabicQuestion_RepliesInArabic()
    {
        var agent = new MockRecommendationsAgent();

        var answer = await agent.GetResponseAsync("ماذا تنصحني؟", "ar");

        Assert.Contains("ننصح", answer);
    }

    [Fact]
    public async Task RecommendationsAgent_EnglishQuestion_RepliesInEnglish()
    {
        var agent = new MockRecommendationsAgent();

        var answer = await agent.GetResponseAsync("Which track?", "en");

        Assert.Contains("Computer Science", answer);
    }

    [Fact]
    public async Task Agents_AreDeterministic_SameInputSameOutput()
    {
        // The previous static mock picked a random string, so a status question could answer
        // "accepted" one moment and "not accepted" the next. Demos need stability.
        var status = new MockStatusAgent();
        var recs = new MockRecommendationsAgent();

        Assert.Equal(await status.GetResponseAsync("q", "en"), await status.GetResponseAsync("q", "en"));
        Assert.Equal(await recs.GetResponseAsync("q", "en"), await recs.GetResponseAsync("q", "en"));
    }

    [Fact]
    public async Task UnknownLanguage_FallsBackToEnglish()
    {
        var agent = new MockStatusAgent();

        var answer = await agent.GetResponseAsync("q", "fr");

        Assert.Contains("under review", answer);
    }
}

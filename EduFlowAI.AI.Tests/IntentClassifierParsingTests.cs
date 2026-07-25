using EduFlowAI.AI.Application.Services;

namespace EduFlowAI.AI.Tests;

public class IntentClassifierParsingTests
{
    [Fact]
    public void PlainCamelCaseJson_ParsesStatusIntent()
    {
        var raw = "{\"primaryIntent\":\"status\",\"confidence\":0.9,\"requiresClarification\":false}";

        var ok = IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.True(ok);
        Assert.Equal("status", dto.PrimaryIntent);
        Assert.Equal("status_service", dto.RoutedTo);
        Assert.Equal(0.9m, dto.Confidence);
    }

    [Fact]
    public void RecommendationIntent_RoutesToRecommendationsAgent()
    {
        var raw = "{\"primaryIntent\":\"recommendation\",\"confidence\":0.8}";

        var ok = IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.True(ok);
        Assert.Equal("recommendations_agent", dto.RoutedTo);
    }

    [Fact]
    public void FencedJsonBlock_IsParsed()
    {
        var raw = "```json\n{\"primaryIntent\":\"knowledge\",\"confidence\":0.95}\n```";

        var ok = IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.True(ok);
        Assert.Equal("knowledge", dto.PrimaryIntent);
        Assert.Equal("knowledge_rag", dto.RoutedTo);
    }

    [Fact]
    public void FencedBlockWithoutLanguage_IsParsed()
    {
        var raw = "```\n{\"primaryIntent\":\"status\",\"confidence\":0.7}\n```";

        var ok = IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.True(ok);
        Assert.Equal("status", dto.PrimaryIntent);
    }

    [Fact]
    public void LeadingProseThenJson_IsParsed()
    {
        var raw = "Sure! Here is the classification:\n{\"primaryIntent\":\"recommendation\",\"confidence\":0.6}";

        var ok = IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.True(ok);
        Assert.Equal("recommendation", dto.PrimaryIntent);
    }

    [Fact]
    public void UppercaseIntent_IsNormalized()
    {
        var raw = "{\"primaryIntent\":\"STATUS\",\"confidence\":0.9}";

        var ok = IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.True(ok);
        Assert.Equal("status", dto.PrimaryIntent);
        Assert.Equal("status_service", dto.RoutedTo);
    }

    [Fact]
    public void ConfidenceAboveOne_IsClampedToOne()
    {
        var raw = "{\"primaryIntent\":\"knowledge\",\"confidence\":1.5}";

        IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.Equal(1m, dto.Confidence);
    }

    [Fact]
    public void NegativeConfidence_IsClampedToZero()
    {
        var raw = "{\"primaryIntent\":\"knowledge\",\"confidence\":-0.4}";

        IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.Equal(0m, dto.Confidence);
    }

    [Theory]
    [InlineData("this is not json at all")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"primaryIntent\":\"weather\",\"confidence\":0.9}")]  // unknown intent value
    [InlineData("{\"confidence\":0.9}")]                                 // missing intent
    [InlineData("{ broken json ")]                                      // malformed
    public void UnparseableOrInvalid_ReturnsFalseAndKnowledgeFallback(string raw)
    {
        var ok = IntentClassifierService.TryParseIntentResponse(raw, out var dto);

        Assert.False(ok);
        Assert.Equal("knowledge", dto.PrimaryIntent);
        Assert.Equal("knowledge_rag", dto.RoutedTo);

        // A parse failure is our defect, not the user's ambiguity: answer from the knowledge base
        // rather than pushing them into a clarification loop. Clarification is reserved for a
        // successful classification whose confidence is below the configured threshold.
        Assert.False(dto.RequiresClarification);
    }

    [Fact]
    public void NullInput_ReturnsFalseAndFallback()
    {
        var ok = IntentClassifierService.TryParseIntentResponse(null, out var dto);

        Assert.False(ok);
        Assert.Equal("knowledge", dto.PrimaryIntent);
    }
}

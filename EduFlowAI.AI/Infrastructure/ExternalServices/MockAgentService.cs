namespace EduFlowAI.AI.Infrastructure.ExternalServices;

public class MockAgentService
{
    public static string GetStatusAgentResponse(string question)
    {
        var responses = new[]
        {
            "Your application is currently under review. We will notify you once a decision has been made.",
            "Your application has been received and is being processed. Expected timeline: 2-4 weeks.",
            "Congratulations! Your application has been accepted. Please proceed to the next step.",
            "We regret to inform you that your application was not accepted. Thank you for applying."
        };
        return responses[new Random().Next(responses.Length)];
    }

    public static string GetRecommendationsAgentResponse(string question)
    {
        var responses = new[]
        {
            "Based on your profile, we recommend Computer Science or Software Engineering programs.",
            "Consider applying to our Business Administration or Management programs.",
            "Your qualifications align well with Engineering or Civil Engineering tracks.",
            "We suggest exploring our Data Science or Artificial Intelligence programs.",
            "Based on your interests, Computer Science and IT programs would be a great fit."
        };
        return responses[new Random().Next(responses.Length)];
    }
}
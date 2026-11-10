using System.Net;
using System.Text.Json;

namespace Services.Tests.Ai.Fakes;

/// <summary>Builds OpenAI chat.completion envelopes.</summary>
public static class OpenAiResponses
{
    public const string ReportedModel = "gpt-4o-mini-2024-07-18";

    public const string ValidAnalysisJson = """
        {
          "categoryCode": "PAYROLL",
          "urgency": "High",
          "confidence": 0.86,
          "suggestedSubject": "הפרש בשכר חודש מאי",
          "summary": "העובד מדווח שהשכר של מאי נמוך מהצפוי.",
          "suggestedSolution": "שלום, בדקנו את תלוש השכר ונחזור אליך עד סוף השבוע.",
          "missingInformation": ["צילום תלוש השכר"]
        }
        """;

    public static HttpResponseMessage Completion(
        string? content, string finishReason = "stop", string? refusal = null,
        int promptTokens = 120, int completionTokens = 80)
    {
        var envelope = new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            created = 1_700_000_000,
            model = ReportedModel,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content, refusal },
                    finish_reason = finishReason,
                    logprobs = (object?)null
                }
            },
            usage = new
            {
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens
            }
        };

        return FakeHttpMessageHandler.Json(HttpStatusCode.OK, JsonSerializer.Serialize(envelope));
    }

    public static HttpResponseMessage ValidCompletion() => Completion(ValidAnalysisJson);
}

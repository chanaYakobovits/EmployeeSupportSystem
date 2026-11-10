using System.Text.Json.Nodes;
using Common.Enums;

namespace Services.Implementations.Ai;

/// <summary>
/// Strict JSON schema for Structured Outputs (spec 5.4). Built per call so <c>categoryCode</c> is limited to the
/// codes of that call. Length and range limits are not expressed here (strict mode does not reliably support them) –
/// <see cref="AiResponseValidator"/> enforces them.
/// </summary>
public static class AiResponseSchema
{
    public const string Name = "ticket_analysis";

    public static BinaryData Build(IEnumerable<string> categoryCodes)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["categoryCode"] = new JsonObject { ["type"] = "string", ["enum"] = ToArray(categoryCodes) },
                ["urgency"] = new JsonObject { ["type"] = "string", ["enum"] = ToArray(Enum.GetNames<Urgency>()) },
                ["confidence"] = new JsonObject { ["type"] = "number" },
                ["suggestedSubject"] = new JsonObject { ["type"] = "string" },
                ["summary"] = new JsonObject { ["type"] = "string" },
                ["suggestedSolution"] = new JsonObject { ["type"] = "string" },
                ["missingInformation"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" }
                }
            },
            ["required"] = ToArray([
                "categoryCode", "urgency", "confidence", "suggestedSubject",
                "summary", "suggestedSolution", "missingInformation"]),
            ["additionalProperties"] = false
        };

        return BinaryData.FromString(schema.ToJsonString());
    }

    private static JsonArray ToArray(IEnumerable<string> values) =>
        new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());
}

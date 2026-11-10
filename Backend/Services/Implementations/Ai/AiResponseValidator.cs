using System.Text.Json;
using Common.DTOs.Ai;
using Common.Enums;

namespace Services.Implementations.Ai;

/// <summary>Result of <see cref="AiResponseValidator.Validate"/>. <see cref="Analysis"/> is null only for Invalid.</summary>
public sealed record AiValidationOutcome(AiValidationVerdict Verdict, AiAnalysis? Analysis);

/// <summary>
/// Spec 5.5: validates and normalizes the model's JSON. Pure – no I/O, no logging, no routing decisions.
/// Order: schema (→ Invalid), length trimming, category (→ UnknownCategory), confidence (→ LowConfidence).
/// </summary>
public sealed class AiResponseValidator
{
    private static readonly AiValidationOutcome InvalidOutcome = new(AiValidationVerdict.Invalid, null);

    public AiValidationOutcome Validate(string json, IReadOnlyCollection<string> allowedCodes, decimal minConfidence)
    {
        ArgumentNullException.ThrowIfNull(allowedCodes);

        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidOutcome;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return InvalidOutcome;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetString(root, "categoryCode", out var categoryCode)
                || !TryGetString(root, "urgency", out var urgencyText)
                || !TryGetConfidence(root, out var confidence)
                || !TryGetString(root, "suggestedSubject", out var subject)
                || !TryGetString(root, "summary", out var summary)
                || !TryGetString(root, "suggestedSolution", out var solution)
                || !TryGetStringArray(root, "missingInformation", out var missingInformation))
            {
                return InvalidOutcome;
            }

            categoryCode = categoryCode.Trim();
            subject = Truncate(subject.Trim(), AiFieldLimits.SubjectMaxLength);
            summary = Truncate(summary.Trim(), AiFieldLimits.SummaryMaxLength);
            solution = Truncate(solution.Trim(), AiFieldLimits.SolutionMaxLength);

            // Exact names only ("High"), no numbers ("3") and no case folding – the schema enum is exact too.
            if (!Enum.GetNames<Urgency>().Contains(urgencyText, StringComparer.Ordinal)
                || categoryCode.Length == 0 || subject.Length == 0 || summary.Length == 0 || solution.Length == 0)
            {
                return InvalidOutcome;
            }

            var analysis = new AiAnalysis(
                categoryCode,
                Enum.Parse<Urgency>(urgencyText),
                confidence,
                subject,
                summary,
                solution,
                missingInformation
                    .Select(item => item.Trim())
                    .Where(item => item.Length > 0)
                    .Take(AiFieldLimits.MissingInfoMaxItems)
                    .Select(item => Truncate(item, AiFieldLimits.MissingInfoItemMaxLength))
                    .ToArray());

            if (!allowedCodes.Contains(categoryCode, StringComparer.Ordinal))
            {
                return new AiValidationOutcome(AiValidationVerdict.UnknownCategory, analysis);
            }

            // Decision: MinConfidence is an inclusive minimum – confidence == MinConfidence (0.6) is Valid,
            // although spec 5.5 says "above 0.6". The setting's name ("Min") reads as inclusive.
            return confidence < minConfidence
                ? new AiValidationOutcome(AiValidationVerdict.LowConfidence, analysis)
                : new AiValidationOutcome(AiValidationVerdict.Valid, analysis);
        }
    }

    /// <summary>Cuts to <paramref name="maxLength"/> UTF-16 units without splitting a surrogate pair.</summary>
    internal static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length].TrimEnd();
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetConfidence(JsonElement root, out decimal confidence)
    {
        confidence = 0;
        return root.TryGetProperty("confidence", out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDecimal(out confidence)
            && confidence is >= 0m and <= 1m;
    }

    private static bool TryGetStringArray(JsonElement root, string name, out List<string> values)
    {
        values = [];
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            values.Add(item.GetString()!);
        }

        return true;
    }
}

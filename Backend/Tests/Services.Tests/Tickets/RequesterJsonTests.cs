using System.Reflection;
using System.Text.Json;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;

namespace Services.Tests.Tickets;

/// <summary>
/// Spec 2.4, 6.3, 7.5: what the requester receives never carries internal data – not hidden on screen, absent from
/// the JSON. Checked on real responses of a fully handled ticket, and structurally on every requester DTO type.
/// </summary>
public sealed class RequesterJsonTests : TicketTestBase
{
    /// <summary>JSON property names that must never reach the requester (camelCase, as the API writes them).</summary>
    private static readonly string[] ForbiddenProperties =
    [
        "urgency", "dueAt", "aiStatus", "aiAnalysis", "classificationSource", "reason", "isInternal",
        "summary", "suggestedSolution", "confidence", "comments", "assignedDepartmentId", "requesterDepartmentId",
    ];

    private const string AiSummary = "סיכום פנימי של ה-AI למטפל";
    private const string AiSolution = "הצעת פתרון של ה-AI";
    private const string InternalComment = "הערה פנימית: לבדוק מול השכר";
    private const string HistoryReason = "נימוק פנימי של המטפל";

    private readonly int _ticketId;

    public RequesterJsonTests()
    {
        _ticketId = AddTicket(NoaId, TicketStatus.InProgress, Now.AddDays(-1), configure: t =>
        {
            t.CategoryId = PayrollId;
            t.Urgency = Urgency.High;
            t.DueAt = Now.AddHours(20);
            t.AiStatus = AiStatus.Completed;
            t.ClassificationSource = ClassificationSource.Ai;
            t.AssignedDepartmentId = HrId;
            t.AssignedToUserId = LiatId;
            t.AiAnalysis = new TicketAiAnalysis
            {
                Model = "m", PromptVersion = "1", CategoryCode = "PAYROLL", Urgency = Urgency.High, Confidence = 0.9m,
                SuggestedSubject = "s", Summary = AiSummary, SuggestedSolution = AiSolution, MissingInfoJson = "[]",
                RedactedInput = "r", CreatedAt = Now,
            };
            t.Comments.Add(new TicketComment { AuthorUserId = LiatId, Body = InternalComment, IsInternal = true, CreatedAt = Now });
            t.StatusHistory.Add(new TicketStatusHistory
            {
                PreviousStatus = TicketStatus.Assigned, NewStatus = TicketStatus.InProgress, ChangedByUserId = LiatId,
                ChangedAt = Now, Reason = HistoryReason,
            });
        });
    }

    [Fact]
    public async Task The_details_json_has_no_internal_fields_or_texts()
    {
        var json = JsonSerializer.Serialize(await ServiceFor(NoaId).GetAsync(_ticketId, CancellationToken.None), ApiJson);

        AssertNoInternalData(json);
        Assert.Contains("ליאת שחר", StringValues(json));   // not vacuous: the ticket is fully handled
    }

    [Fact]
    public async Task The_list_json_has_no_internal_fields_or_texts()
    {
        var json = JsonSerializer.Serialize(
            await ServiceFor(NoaId).GetMyPageAsync(new MyTicketsQuery(), CancellationToken.None), ApiJson);

        AssertNoInternalData(json);
        Assert.Contains($"\"id\":{_ticketId}", json);
    }

    [Fact]
    public async Task The_summary_json_has_only_the_three_counters()
    {
        var json = JsonSerializer.Serialize(await ServiceFor(NoaId).GetMySummaryAsync(CancellationToken.None), ApiJson);

        Assert.Equal(["closedThisMonth", "open", "waitingForMe"], PropertyNames(json).Order());
    }

    /// <summary>Also catches a field added to a requester DTO in the future, before any test data has it.</summary>
    [Fact]
    public void No_requester_dto_type_declares_an_internal_property()
    {
        Type[] roots =
        [
            typeof(TicketCreatedDto), typeof(RequesterTicketListItemDto), typeof(RequesterTicketsSummaryDto),
            typeof(RequesterTicketDetailsDto),
        ];

        var names = roots.SelectMany(AllPropertyNames).Select(n => JsonNamingPolicy.CamelCase.ConvertName(n)).ToList();

        Assert.NotEmpty(names);
        Assert.Empty(names.Intersect(ForbiddenProperties, StringComparer.OrdinalIgnoreCase));
    }

    private static void AssertNoInternalData(string json)
    {
        var names = PropertyNames(json).ToList();
        Assert.Empty(names.Intersect(ForbiddenProperties, StringComparer.OrdinalIgnoreCase));

        // Compared after decoding: the serializer escapes Hebrew (\uXXXX), so a raw substring check would be vacuous.
        var values = StringValues(json).ToList();
        foreach (var text in new[] { AiSummary, AiSolution, InternalComment, HistoryReason, "High", "Critical" })
        {
            Assert.DoesNotContain(values, value => value.Contains(text));
        }
    }

    /// <summary>Every property name at any depth of the JSON document.</summary>
    private static IEnumerable<string> PropertyNames(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Walk(document.RootElement).ToList();

        static IEnumerable<string> Walk(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Walk(p.Value).Prepend(p.Name)),
            JsonValueKind.Array => element.EnumerateArray().SelectMany(Walk),
            _ => [],
        };
    }

    /// <summary>Every string value at any depth of the JSON document, decoded.</summary>
    private static IEnumerable<string> StringValues(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Walk(document.RootElement).ToList();

        static IEnumerable<string> Walk(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Walk(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().SelectMany(Walk),
            JsonValueKind.String => [element.GetString()!],
            _ => [],
        };
    }

    /// <summary>Property names of a type and of the DTO types it contains (lists included), recursively.</summary>
    private static IEnumerable<string> AllPropertyNames(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return property.Name;

            var nested = property.PropertyType.IsGenericType
                ? property.PropertyType.GetGenericArguments()[0]
                : property.PropertyType;
            if (nested.Namespace == typeof(RequesterTicketDetailsDto).Namespace && nested != type)
            {
                foreach (var name in AllPropertyNames(nested))
                {
                    yield return name;
                }
            }
        }
    }
}

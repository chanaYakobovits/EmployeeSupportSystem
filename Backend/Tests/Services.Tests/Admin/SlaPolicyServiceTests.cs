using Common.Constants;
using Common.DTOs.Sla;
using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Services.Validators;

namespace Services.Tests.Admin;

public sealed class SlaPolicyServiceTests : AdminTestBase
{
    private Task<SlaPoliciesDto> Update(int? critical, int? high, int? medium, int? low) =>
        SlaPolicyService.UpdateAsync(new UpdateSlaPoliciesRequest(critical, high, medium, low), Ip, CancellationToken.None);

    private int HoursOf(Urgency urgency) => SlaPolicies.Policies.Single(p => p.Urgency == urgency).ResolutionHours;

    [Fact]
    public async Task Get_returns_the_four_levels()
    {
        var result = await SlaPolicyService.GetAsync(CancellationToken.None);

        Assert.Equal(new SlaPoliciesDto(4, 24, 72, 120), result);
    }

    [Fact]
    public async Task A_missing_level_gets_the_default()
    {
        SlaPolicies.Policies.RemoveAll(p => p.Urgency == Urgency.Low);

        var result = await SlaPolicyService.GetAsync(CancellationToken.None);

        Assert.Equal(SlaDefaults.Hours[Urgency.Low], result.Low);
    }

    [Fact]
    public async Task Update_saves_all_levels_and_is_audited()
    {
        var result = await Update(2, 8, 48, 96);

        Assert.Equal(new SlaPoliciesDto(2, 8, 48, 96), result);
        Assert.Equal(48, HoursOf(Urgency.Medium));
        var audit = Assert.Single(AuditLog.Entries);
        Assert.Equal((AuditActions.SlaPoliciesUpdated, AuditEntityTypes.SlaPolicy), (audit.Action, audit.EntityType));
        Assert.Null(audit.EntityId);
    }

    [Fact]
    public async Task Equal_levels_are_allowed()
    {
        var result = await Update(24, 24, 24, 24);

        Assert.Equal(24, result.Critical);
    }

    [Fact]
    public async Task A_missing_row_is_added()
    {
        SlaPolicies.Policies.RemoveAll(p => p.Urgency == Urgency.Critical);

        await Update(3, 24, 72, 120);

        Assert.Equal(3, HoursOf(Urgency.Critical));
    }

    [Theory]
    [InlineData(25, 24, 72, 120, "high", "גבוהה", "קריטית")]
    [InlineData(4, 80, 72, 120, "medium", "בינונית", "גבוהה")]
    [InlineData(4, 24, 72, 48, "low", "נמוכה", "בינונית")]
    public async Task Out_of_order_is_reported_on_the_less_urgent_level(int critical, int high, int medium, int low,
        string field, string level, string moreUrgent)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => Update(critical, high, medium, low));

        Assert.Equal([UpdateSlaPoliciesRequestValidator.OrderMessage(level, moreUrgent)], exception.Errors[field]);
        Assert.Single(exception.Errors);
        Assert.Equal(4, HoursOf(Urgency.Critical)); // nothing saved
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(UpdateSlaPoliciesRequestValidator.MaxHours + 1)]
    public async Task Hours_must_be_positive_and_at_most_a_year(int critical)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => Update(critical, 24, 72, 120));

        Assert.True(exception.Errors.ContainsKey("critical"));
        Assert.Single(exception.Errors); // no order error on top of it
    }

    [Fact]
    public async Task Every_level_is_required()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => Update(4, null, 72, 120));

        Assert.True(exception.Errors.ContainsKey("high"));
    }

    [Fact]
    public async Task Existing_rows_are_updated_in_place()
    {
        var before = SlaPolicies.Policies.ToList();

        await Update(4, 24, 48, 120);

        Assert.Equal(before.Select(p => p.Id), SlaPolicies.Policies.Select(p => p.Id));
        Assert.Equal(48, before.Single(p => p.Urgency == Urgency.Medium).ResolutionHours);
    }
}

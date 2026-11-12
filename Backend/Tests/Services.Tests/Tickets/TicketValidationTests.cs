using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Services.Validators;

namespace Services.Tests.Tickets;

/// <summary>Spec 3.1, 7.4, 9.4: subject up to 150, description 10–4,000 (after trimming), list paging.</summary>
public sealed class TicketValidationTests : TicketTestBase
{
    private readonly CreateTicketRequestValidator _create = new();
    private readonly MyTicketsQueryValidator _list = new();

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public void Subject_is_at_most_150_characters(int length, bool valid)
    {
        var result = _create.Validate(new CreateTicketRequest { Subject = new string('א', length), Description = "תיאור תקין לגמרי" });

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(CreateTicketRequestValidator.SubjectLengthMessage, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void Description_is_10_to_4000_characters(int length, bool valid)
    {
        var result = _create.Validate(new CreateTicketRequest { Description = new string('א', length) });

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(CreateTicketRequestValidator.DescriptionLengthMessage, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Fact]
    public void Surrounding_whitespace_does_not_count_toward_the_length()
    {
        var result = _create.Validate(new CreateTicketRequest { Description = "   " + new string('א', 9) + "\n\n  " });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    \n\t  ")]
    public async Task An_empty_or_whitespace_description_is_rejected_with_a_field_error(string? description)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(NoaId).CreateAsync(new CreateTicketRequest { Description = description! }, CancellationToken.None));

        Assert.Equal([CreateTicketRequestValidator.DescriptionRequiredMessage], error.Errors!["description"]);
    }

    [Fact]
    public async Task Errors_are_keyed_by_the_camelCase_field_names()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = new string('א', 151), Description = "קצר" }, CancellationToken.None));

        Assert.Equal(["description", "subject"], error.Errors!.Keys.Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(500)]
    public async Task An_invalid_page_size_is_rejected(int pageSize)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(NoaId).GetMyPageAsync(new MyTicketsQuery { PageSize = pageSize }, CancellationToken.None));

        Assert.True(error.Errors!.ContainsKey("pageSize"));
    }

    [Theory]
    [InlineData("page", "0", null, null)]
    [InlineData("sortBy", null, "dueAt", null)]
    [InlineData("sortDir", null, null, "up")]
    public void Page_sort_field_and_direction_are_validated(string field, string? page, string? sortBy, string? sortDir)
    {
        var result = _list.Validate(new MyTicketsQuery
        {
            Page = page is null ? 1 : int.Parse(page), SortBy = sortBy, SortDir = sortDir,
        });

        Assert.Equal(field, Assert.Single(result.Errors).PropertyName, ignoreCase: true);
    }

    [Fact]
    public void An_unknown_status_value_is_rejected()
    {
        var result = _list.Validate(new MyTicketsQuery { Status = [TicketStatus.New, (TicketStatus)99] });

        Assert.False(result.IsValid);
    }
}

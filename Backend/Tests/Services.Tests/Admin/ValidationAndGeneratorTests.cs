using Common.Constants;
using Common.DTOs.Users;
using Services.Implementations;
using Services.Validators;

namespace Services.Tests.Admin;

public sealed class PageQueryValidationTests
{
    private readonly UserListQueryValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(100)]
    public void Page_sizes_from_1_to_100_are_valid(int pageSize) =>
        Assert.True(_validator.Validate(new UserListQuery { PageSize = pageSize }).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    [InlineData(500)]
    public void Page_sizes_outside_1_to_100_are_invalid(int pageSize)
    {
        var result = _validator.Validate(new UserListQuery { PageSize = pageSize });

        var error = Assert.Single(result.Errors);
        Assert.Equal("PageSize", error.PropertyName);
        Assert.Equal(PageQueryValidator<UserListQuery>.PageSizeMessage, error.ErrorMessage);
    }

    [Fact]
    public void Defaults_are_page_1_and_25_rows() =>
        Assert.Equal((1, 25), (new UserListQuery().Page, new UserListQuery().PageSize));

    [Fact]
    public void Page_must_be_at_least_1() =>
        Assert.False(_validator.Validate(new UserListQuery { Page = 0 }).IsValid);

    [Theory]
    [InlineData("name")]
    [InlineData("employeeCode")]
    [InlineData("lastLoginAt")]
    [InlineData(null)]
    public void SortBy_from_the_closed_list_is_valid(string? sortBy) =>
        Assert.True(_validator.Validate(new UserListQuery { SortBy = sortBy }).IsValid);

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("Name; DROP TABLE Users")]
    public void SortBy_outside_the_closed_list_is_invalid(string sortBy) =>
        Assert.False(_validator.Validate(new UserListQuery { SortBy = sortBy }).IsValid);

    [Theory]
    [InlineData("asc", true)]
    [InlineData("DESC", true)]
    [InlineData("up", false)]
    public void SortDir_is_asc_or_desc(string sortDir, bool valid) =>
        Assert.Equal(valid, _validator.Validate(new UserListQuery { SortDir = sortDir }).IsValid);

    [Theory]
    [InlineData("PageSize", "pageSize")]
    [InlineData("Roles[0]", "roles[0]")]
    [InlineData("Address.ZipCode", "address.zipCode")]
    public void Error_keys_are_camelCase_like_the_JSON_body(string property, string expected) =>
        Assert.Equal(expected, ValidatorExtensions.ToCamelCasePath(property));
}

public sealed class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void Every_temporary_password_meets_the_password_policy_and_they_do_not_repeat()
    {
        var passwords = Enumerable.Range(0, 1000).Select(_ => TemporaryPasswordGenerator.Generate()).ToList();

        Assert.All(passwords, p =>
        {
            Assert.Equal(TemporaryPasswordGenerator.Length, p.Length);
            Assert.Empty(PasswordPolicy.Validate(p));
        });
        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }

    [Fact]
    public void Look_alike_characters_are_never_used()
    {
        var all = string.Concat(Enumerable.Range(0, 500).Select(_ => TemporaryPasswordGenerator.Generate()));

        Assert.DoesNotContain(all, c => "0O1lI".Contains(c));
    }
}

public sealed class EmployeeCodeFormatTests
{
    [Theory]
    [InlineData(1, "EMP-00001")]
    [InlineData(42, "EMP-00042")]
    [InlineData(99999, "EMP-99999")]
    [InlineData(123456, "EMP-123456")]
    public void Codes_are_EMP_and_at_least_five_digits(long number, string expected) =>
        Assert.Equal(expected, EmployeeCodeFormat.Format(number));
}

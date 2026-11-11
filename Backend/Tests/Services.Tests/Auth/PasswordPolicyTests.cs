using Services.Implementations;

namespace Services.Tests.Auth;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdefg1")]       // exactly 8
    [InlineData("password123")]
    [InlineData("שלוםעולם7")]      // Hebrew letters count as letters
    [InlineData("12345678a")]
    [InlineData("  spaces 1 ok")]
    public void Valid_passwords_pass(string password)
    {
        Assert.Empty(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("Abc1", PasswordPolicy.TooShort)]
    [InlineData("Abcdef1", PasswordPolicy.TooShort)]  // 7
    [InlineData("12345678", PasswordPolicy.MissingLetter)]
    [InlineData("abcdefgh", PasswordPolicy.MissingDigit)]
    [InlineData("!@#$%^&*()", PasswordPolicy.MissingLetter)]
    public void Invalid_passwords_report_the_broken_rule(string password, string expectedError)
    {
        Assert.Contains(expectedError, PasswordPolicy.Validate(password));
    }

    [Fact]
    public void Every_broken_rule_is_reported()
    {
        Assert.Equal(
            [PasswordPolicy.TooShort, PasswordPolicy.MissingLetter, PasswordPolicy.MissingDigit],
            PasswordPolicy.Validate("!!"));
    }

    [Fact]
    public void Too_long_password_is_rejected()
    {
        Assert.Contains(PasswordPolicy.TooLong, PasswordPolicy.Validate(new string('a', 128) + "1"));
        Assert.Empty(PasswordPolicy.Validate(new string('a', 127) + "1"));
    }

    [Fact]
    public void Same_as_current_is_case_sensitive()
    {
        Assert.Equal([PasswordPolicy.SameAsCurrent], PasswordPolicy.Validate("Demo1234", "Demo1234"));
        Assert.Empty(PasswordPolicy.Validate("demo1234", "Demo1234"));
    }
}

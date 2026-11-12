using Common.Constants;
using Common.Exceptions;

namespace Services.Tests.Auth;

/// <summary>An invited user has no password yet (PasswordHash null): no password signs in, and it looks like any failure.</summary>
public sealed class LoginWithoutPasswordTests : AuthServiceTestBase
{
    [Theory]
    [InlineData(Password)]
    [InlineData("anything1")]
    [InlineData(FakePasswordHasherPrefixGuess)]
    public async Task No_password_signs_in_a_user_who_has_none(string password)
    {
        Noa.PasswordHash = null;

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", password));

        Assert.Empty(RefreshTokens.Tokens);
    }

    // The dummy hash is a hash of random values; even its "obvious" preimage must not match.
    private const string FakePasswordHasherPrefixGuess = "HASH:";

    [Fact]
    public async Task The_failure_is_the_uniform_one_counted_for_lockout_and_audited()
    {
        Noa.PasswordHash = null;
        var verifiesBefore = Hasher.VerifyCount;

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", Password));

        Assert.Equal(new AuthenticationFailedException().Title, exception.Title);
        Assert.Equal(verifiesBefore + 1, Hasher.VerifyCount); // same work as a wrong password (dummy hash)
        Assert.Equal(1, Noa.FailedLoginCount);
        Assert.Equal(AuditActions.LoginFailedPasswordNotSet, Assert.Single(AuditLog.Entries).Action);
    }
}

using System.Net.Sockets;
using Common.Enums;
using Common.Models;
using Common.Options;
using MailKit.Net.Smtp;
using MimeKit;
using Services.Implementations.Email;
using Services.Implementations.Invitations;

namespace Services.Tests.Invitations;

/// <summary>The e-mail content and the SMTP error mapping.</summary>
public sealed class InvitationEmailTests
{
    private static readonly DateTime ExpiresAtUtc = new(2026, 11, 15, 7, 30, 0, DateTimeKind.Utc); // 09:30 in Israel

    private static readonly User Employee = new()
    {
        Id = 26, FirstName = "נגה", LastName = "ברק", EmployeeCode = "EMP-00026", Email = "noga.barak@example.com",
    };

    private static InvitationEmailComposer Composer(string baseUrl = "https://ovdenet.example.com/") =>
        new(Microsoft.Extensions.Options.Options.Create(new InvitationOptions { FrontendBaseUrl = baseUrl }));

    [Fact]
    public void The_email_is_Hebrew_right_to_left_with_name_code_link_expiry_and_the_ignore_sentence()
    {
        var message = Composer().Compose(Employee, "TOKEN_value-1", ExpiresAtUtc);

        Assert.Equal("noga.barak@example.com", message.ToAddress);
        Assert.Contains("dir=\"rtl\"", message.HtmlBody);
        Assert.Contains("lang=\"he\"", message.HtmlBody);
        Assert.Contains("נגה", message.HtmlBody);
        Assert.Contains("EMP-00026", message.HtmlBody);
        Assert.Contains("15/11/2026 09:30", message.HtmlBody);
        Assert.Contains("אם לא ציפית למייל הזה", message.HtmlBody);
        Assert.Contains("אם לא ציפית למייל הזה", message.TextBody);
        Assert.Contains("https://ovdenet.example.com/set-password#token=TOKEN_value-1", message.TextBody);
        Assert.Contains("https://ovdenet.example.com/set-password#token=TOKEN_value-1", message.HtmlBody);
    }

    [Fact]
    public void The_token_goes_after_the_hash_never_in_the_query_string()
    {
        var link = InvitationEmailComposer.BuildLink("http://localhost:4200", "abc");

        Assert.Equal("http://localhost:4200/set-password#token=abc", link);
        Assert.DoesNotContain("?", link);
    }

    [Fact]
    public void Names_are_html_encoded()
    {
        var attacker = new User
        {
            FirstName = "<script>alert(1)</script>", LastName = "x", EmployeeCode = "EMP-00027", Email = "a@example.com",
        };

        var message = Composer().Compose(attacker, "t", ExpiresAtUtc);

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }

    [Fact]
    public void The_email_contains_no_password()
    {
        var message = Composer().Compose(Employee, "t", ExpiresAtUtc);

        Assert.DoesNotContain("סיסמה זמנית", message.HtmlBody);
        Assert.DoesNotContain("Demo1234", message.HtmlBody);
    }

    [Fact]
    public void The_mime_message_is_utf8_html_with_a_text_alternative()
    {
        var message = Composer().Compose(Employee, "t", ExpiresAtUtc);
        var options = new EmailOptions { FromAddress = "no-reply@example.com", FromName = "עובדנט" };

        using var mime = MimeMessageFactory.Create(message, options);

        Assert.Equal(InvitationEmailComposer.Subject, mime.Subject);
        Assert.Equal("no-reply@example.com", ((MailboxAddress)mime.From[0]).Address);
        Assert.NotNull(mime.HtmlBody);
        Assert.NotNull(mime.TextBody);
    }

    [Fact]
    public void A_recipient_rejected_for_good_is_permanent()
    {
        var error = SmtpEmailSender.Translate(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted,
            SmtpStatusCode.MailboxUnavailable, new MailboxAddress("", "x@example.com"), "550 no such user x@example.com"));

        Assert.Equal(EmailErrorCode.RecipientRejected, error.Code);
        Assert.False(error.IsTransient);
        Assert.DoesNotContain("x@example.com", error.Message); // the address never leaves the sender
    }

    [Fact]
    public void Connection_problems_and_timeouts_are_transient()
    {
        var connection = SmtpEmailSender.Translate(new SocketException((int)SocketError.ConnectionRefused));
        var timeout = SmtpEmailSender.Translate(new TimeoutException());

        Assert.Equal(EmailErrorCode.SmtpConnectionFailed, connection.Code);
        Assert.True(connection.IsTransient);
        Assert.Equal(EmailErrorCode.Timeout, timeout.Code);
        Assert.True(timeout.IsTransient);
    }
}

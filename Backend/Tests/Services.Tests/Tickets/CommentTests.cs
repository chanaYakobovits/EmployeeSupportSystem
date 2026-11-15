using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Services.Implementations.Tickets;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Tickets;

/// <summary>POST /api/tickets/{id}/comments and /attachments: who may write what, and when (spec 2.4, 3.2, 6.3).</summary>
public sealed class CommentTests : TicketTestBase
{
    private const string Body = "מצרפת את התלוש המלא";

    /// <summary>Noa's ticket, handled by HR (Liat) – in scope for Liat, Rina (HR manager), Ron (Development manager), Avner.</summary>
    private readonly int _ticketId;

    public CommentTests()
    {
        _ticketId = AddTicket(NoaId, TicketStatus.InProgress, Now.AddDays(-1), configure: t =>
        {
            t.AssignedDepartmentId = HrId;
            t.AssignedToUserId = LiatId;
        });
    }

    [Fact]
    public async Task The_requester_adds_a_visible_comment_with_files()
    {
        var created = await ConversationFor(NoaId).AddCommentAsync(_ticketId, Comment(),
            [TestFiles.PdfFile("תלוש.pdf"), TestFiles.File("צילום.png", TestFiles.Png)], CancellationToken.None);

        var comment = AllComments().Single(c => c.Id == created.Id);
        Assert.Equal((NoaId, Body, false, false), (comment.AuthorUserId, comment.Body, comment.IsInternal, comment.IsResolution));
        Assert.Equal(Now, comment.CreatedAt);
        Assert.Equal(["תלוש.pdf", "צילום.png"], comment.Attachments.OrderBy(a => a.Id).Select(a => a.OriginalFileName));
        Assert.All(comment.Attachments, a => Assert.Equal(_ticketId, a.TicketId));
        Assert.Equal(["application/pdf", "image/png"], comment.Attachments.OrderBy(a => a.Id).Select(a => a.ContentType));
        Assert.All(comment.Attachments, a => Assert.True(Files.Files.ContainsKey(a.StoredFileName)));
    }

    [Fact]
    public async Task The_body_is_stored_trimmed()
    {
        var created = await ConversationFor(NoaId).AddCommentAsync(_ticketId, Comment("  " + Body + "\n "), [],
            CancellationToken.None);

        Assert.Equal(Body, AllComments().Single(c => c.Id == created.Id).Body);
    }

    [Fact]
    public async Task The_handler_writes_an_internal_note()
    {
        var created = await ConversationFor(LiatId).AddCommentAsync(_ticketId, Comment(isInternal: true),
            [TestFiles.PdfFile()], CancellationToken.None);

        var note = AllComments().Single(c => c.Id == created.Id);
        Assert.True(note.IsInternal);
        Assert.Single(note.Attachments);
    }

    [Fact]
    public async Task The_requester_cannot_write_an_internal_note()
    {
        var error = await Assert.ThrowsAsync<ForbiddenException>(() => ConversationFor(NoaId).AddCommentAsync(_ticketId,
            Comment(isInternal: true), [TestFiles.PdfFile()], CancellationToken.None));

        Assert.Equal("internal-comment-not-allowed", error.ErrorType);
        Assert.Empty(AllComments());
        Assert.Empty(Files.Files);
    }

    [Fact]
    public async Task A_department_manager_writes_internal_notes_but_does_not_answer_the_employee()
    {
        await ConversationFor(RinaId).AddCommentAsync(_ticketId, Comment(isInternal: true), [], CancellationToken.None);
        await ConversationFor(RonId).AddCommentAsync(_ticketId, Comment(isInternal: true), [], CancellationToken.None);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            ConversationFor(RonId).AddCommentAsync(_ticketId, Comment(), [], CancellationToken.None));
        Assert.Equal("reply-not-allowed", error.ErrorType);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ConversationFor(RinaId).AddAttachmentsAsync(_ticketId, [TestFiles.PdfFile()], CancellationToken.None));
    }

    [Fact]
    public async Task The_executive_only_views()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ConversationFor(AvnerId).AddCommentAsync(_ticketId, Comment(), [], CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ConversationFor(AvnerId).AddCommentAsync(_ticketId, Comment(isInternal: true), [], CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ConversationFor(AvnerId).AddAttachmentsAsync(_ticketId, [TestFiles.PdfFile()], CancellationToken.None));

        Assert.Empty(AllComments());
    }

    [Fact]
    public async Task Another_employee_gets_not_found_not_forbidden()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ConversationFor(IdoId).AddCommentAsync(_ticketId, Comment(isInternal: true), [], CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ConversationFor(AlonId).AddCommentAsync(_ticketId, Comment(), [], CancellationToken.None));
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task A_closed_or_cancelled_ticket_takes_no_comments_or_files(TicketStatus status)
    {
        var id = AddTicket(NoaId, status, Now.AddDays(-3), configure: t => t.AssignedToUserId = LiatId);

        var comment = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ConversationFor(NoaId).AddCommentAsync(id, Comment(), [TestFiles.PdfFile()], CancellationToken.None));
        var note = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ConversationFor(LiatId).AddCommentAsync(id, Comment(isInternal: true), [], CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ConversationFor(NoaId).AddAttachmentsAsync(id, [TestFiles.PdfFile()], CancellationToken.None));

        Assert.Equal("ticket-closed", comment.ErrorType);
        Assert.Equal(TicketConversationService.TicketClosedTitle, note.Title);
        Assert.Empty(AllComments());
        Assert.Empty(Files.Files);
    }

    [Fact]
    public async Task A_visible_comment_updates_the_ticket_an_internal_note_does_not()
    {
        await ConversationFor(LiatId).AddCommentAsync(_ticketId, Comment(isInternal: true), [], CancellationToken.None);
        Assert.Equal(Now.AddDays(-1), AllTickets().Single(t => t.Id == _ticketId).UpdatedAt);

        Clock.Advance(TimeSpan.FromMinutes(5));
        await ConversationFor(NoaId).AddCommentAsync(_ticketId, Comment(), [], CancellationToken.None);
        Assert.Equal(Now.AddMinutes(5), AllTickets().Single(t => t.Id == _ticketId).UpdatedAt);

        Clock.Advance(TimeSpan.FromMinutes(5));
        await ConversationFor(NoaId).AddAttachmentsAsync(_ticketId, [TestFiles.PdfFile()], CancellationToken.None);
        Assert.Equal(Now.AddMinutes(10), AllTickets().Single(t => t.Id == _ticketId).UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    public async Task An_empty_body_is_rejected(string body)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            ConversationFor(NoaId).AddCommentAsync(_ticketId, Comment(body), [], CancellationToken.None));

        Assert.Equal([CreateCommentRequestValidator.BodyRequiredMessage], error.Errors["body"]);
    }

    [Fact]
    public async Task A_body_over_4000_characters_is_rejected_and_4000_pass()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => ConversationFor(NoaId).AddCommentAsync(_ticketId,
            Comment(new string('א', 4001)), [], CancellationToken.None));
        Assert.Equal([CreateCommentRequestValidator.BodyLengthMessage], error.Errors["body"]);

        await ConversationFor(NoaId).AddCommentAsync(_ticketId, Comment(new string('א', 4000)), [], CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_files_are_rejected_before_anything_is_written()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => ConversationFor(NoaId).AddCommentAsync(_ticketId,
            Comment(), [TestFiles.PdfFile(), TestFiles.File("fake.pdf", TestFiles.Text)], CancellationToken.None));

        Assert.Equal([AttachmentFilesValidator.ContentMismatchMessage], error.Errors["files[1]"]);
        Assert.Empty(Files.Files);
        Assert.Empty(AllComments());
    }

    [Fact]
    public async Task Attachments_without_a_comment_are_listed_back()
    {
        var added = await ConversationFor(NoaId).AddAttachmentsAsync(_ticketId,
            [TestFiles.PdfFile("א.pdf"), TestFiles.File("ב.docx", TestFiles.Docx)], CancellationToken.None);

        Assert.Equal(["א.pdf", "ב.docx"], added.Select(a => a.FileName));
        Assert.All(added, a => Assert.Equal("נועה ברקוביץ", a.UploadedByName));
        Assert.All(AllAttachments(), a => Assert.Null(a.CommentId));
    }

    [Fact]
    public async Task Adding_attachments_requires_at_least_one_file()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            ConversationFor(NoaId).AddAttachmentsAsync(_ticketId, [], CancellationToken.None));

        Assert.Equal([TicketConversationService.NoFilesMessage], error.Errors["files"]);
    }

    [Fact]
    public async Task The_log_has_ids_sizes_and_types_never_the_body_or_file_name()
    {
        var created = await ConversationFor(NoaId).AddCommentAsync(_ticketId, Comment("תוכן סודי של התגובה"),
            [TestFiles.PdfFile("שם קובץ סודי.pdf")], CancellationToken.None);

        var log = ConversationLogger.AllText;
        Assert.Contains(created.Id.ToString(), log);
        Assert.Contains("application/pdf", log);
        Assert.Contains(TestFiles.Pdf.Length.ToString(), log);
        Assert.DoesNotContain("תוכן סודי", log);
        Assert.DoesNotContain("שם קובץ סודי", log);
    }

    private static CreateCommentRequest Comment(string body = Body, bool isInternal = false) =>
        new() { Body = body, IsInternal = isInternal };
}

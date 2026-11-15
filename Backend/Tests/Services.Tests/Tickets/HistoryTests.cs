using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Models;

namespace Services.Tests.Tickets;

/// <summary>
/// GET /api/tickets/{id}/history and the attachment download (spec 6.3, 7.5, 7.7): the requester never receives an
/// internal note, a file of one, or a reason – neither in the timeline nor by downloading.
/// </summary>
public sealed class HistoryTests : TicketTestBase
{
    private const string InternalNote = "הערה פנימית: לבדוק מול השכר";
    private const string Reason = "נימוק פנימי לשינוי";

    private readonly int _ticketId;
    private readonly int _openingFile;
    private readonly int _replyFile;
    private readonly int _internalFile;
    private readonly int _internalNoteId;

    public HistoryTests()
    {
        var created = Now.AddHours(-5);
        _ticketId = AddTicket(NoaId, TicketStatus.InProgress, created, configure: t =>
        {
            t.AssignedDepartmentId = HrId;
            t.AssignedToUserId = LiatId;
            t.StatusHistory.Add(new TicketStatusHistory { NewStatus = TicketStatus.New, ChangedByUserId = NoaId, ChangedAt = created });
            t.StatusHistory.Add(new TicketStatusHistory
            {
                PreviousStatus = TicketStatus.New, NewStatus = TicketStatus.InProgress, ChangedByUserId = LiatId,
                ChangedAt = Now.AddHours(-3), Reason = Reason,
            });
        });

        _openingFile = AddAttachmentAt(created, commentId: null, "מסמך פתיחה.pdf");
        var question = AddComment(_ticketId, LiatId, "חסר תלוש", at: Now.AddHours(-2));
        var reply = AddComment(_ticketId, NoaId, "מצרפת", at: Now.AddHours(-1));
        _replyFile = AddAttachmentAt(Now.AddHours(-1), reply, "תלוש.pdf");
        _internalNoteId = AddComment(_ticketId, LiatId, InternalNote, isInternal: true, at: Now.AddMinutes(-30));
        _internalFile = AddAttachmentAt(Now.AddMinutes(-30), _internalNoteId, "בדיקה פנימית.pdf");
        Assert.NotEqual(question, reply);
    }

    [Fact]
    public async Task The_requester_gets_their_timeline_in_order_without_internal_notes_files_or_reasons()
    {
        var history = Assert.IsType<RequesterTicketHistoryDto>(
            await ConversationFor(NoaId).GetHistoryAsync(_ticketId, CancellationToken.None));

        Assert.Equal(_ticketId, history.TicketId);
        Assert.Collection(history.Items,
            item => Assert.Equal(TicketStatus.New, Assert.IsType<RequesterTimelineStatusDto>(item).NewStatus),
            item => Assert.Equal(_openingFile, Assert.IsType<RequesterTimelineAttachmentDto>(item).Attachment.Id),
            item => Assert.Equal(TicketStatus.InProgress, Assert.IsType<RequesterTimelineStatusDto>(item).NewStatus),
            item =>
            {
                var question = Assert.IsType<RequesterTimelineCommentDto>(item);
                Assert.Equal(("חסר תלוש", "ליאת שחר", false), (question.Body, question.AuthorName, question.IsFromRequester));
            },
            item =>
            {
                var reply = Assert.IsType<RequesterTimelineCommentDto>(item);
                Assert.True(reply.IsFromRequester);
                Assert.Equal([_replyFile], reply.Attachments.Select(a => a.Id));
            });
    }

    [Fact]
    public async Task The_handler_gets_the_full_timeline_with_internal_notes_their_files_and_reasons()
    {
        var history = Assert.IsType<StaffTicketHistoryDto>(
            await ConversationFor(LiatId).GetHistoryAsync(_ticketId, CancellationToken.None));

        Assert.Equal(6, history.Items.Count);
        Assert.Equal(Reason, history.Items.OfType<StaffTimelineStatusDto>().Single(s => s.NewStatus == TicketStatus.InProgress).Reason);
        var note = Assert.IsType<StaffTimelineCommentDto>(history.Items[^1]);
        Assert.True(note.IsInternal);
        Assert.Equal(InternalNote, note.Body);
        Assert.Equal([_internalFile], note.Attachments.Select(a => a.Id));
        Assert.Equal(history.Items.Select(i => i.At).Order(), history.Items.Select(i => i.At));
    }

    [Theory]
    [InlineData(RinaId)]
    [InlineData(RonId)]
    [InlineData(AvnerId)]
    public async Task Managers_and_the_executive_see_internal_notes(int userId)
    {
        var history = await ConversationFor(userId).GetHistoryAsync(_ticketId, CancellationToken.None);

        Assert.Contains(Assert.IsType<StaffTicketHistoryDto>(history).Items.OfType<StaffTimelineCommentDto>(),
            c => c.IsInternal);
    }

    [Fact]
    public async Task Outside_the_scope_the_history_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ConversationFor(IdoId).GetHistoryAsync(_ticketId, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => ConversationFor(AlonId).GetHistoryAsync(_ticketId, CancellationToken.None));
    }

    [Fact]
    public async Task The_requester_downloads_visible_files()
    {
        var download = await ConversationFor(NoaId).DownloadAttachmentAsync(_ticketId, _replyFile, "10.0.0.1",
            CancellationToken.None);

        await using var content = download.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        Assert.Equal(("תלוש.pdf", "application/pdf"), (download.FileName, download.ContentType));
        Assert.Equal(Fakes.TestFiles.Pdf, copy.ToArray());
    }

    [Fact]
    public async Task The_requester_cannot_download_a_file_of_an_internal_note_it_is_simply_not_found()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            ConversationFor(NoaId).DownloadAttachmentAsync(_ticketId, _internalFile, "10.0.0.1", CancellationToken.None));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            ConversationFor(NoaId).DownloadAttachmentAsync(_ticketId, 987654, "10.0.0.1", CancellationToken.None));

        Assert.Equal(missing.Title, error.Title);
        Assert.Empty(AllAuditLogs());
    }

    [Fact]
    public async Task The_handler_downloads_a_file_of_an_internal_note_and_the_download_is_audited()
    {
        var download = await ConversationFor(LiatId).DownloadAttachmentAsync(_ticketId, _internalFile, "10.0.0.7",
            CancellationToken.None);
        await download.Content.DisposeAsync();

        var audit = Assert.Single(AllAuditLogs());
        Assert.Equal((AuditActions.AttachmentDownloaded, AuditEntityTypes.TicketAttachment, _internalFile, LiatId, "10.0.0.7", OrgA),
            (audit.Action, audit.EntityType, audit.EntityId, audit.UserId, audit.IpAddress, audit.OrganizationId));
    }

    [Fact]
    public async Task A_file_of_another_ticket_is_not_found_through_this_one()
    {
        var other = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-1));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ConversationFor(NoaId).DownloadAttachmentAsync(other, _replyFile, "10.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task Another_employee_cannot_download_even_a_visible_file()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ConversationFor(IdoId).DownloadAttachmentAsync(_ticketId, _openingFile, "10.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task A_row_whose_stored_file_is_missing_is_not_found_and_not_audited()
    {
        var row = AllAttachments().Single(a => a.Id == _openingFile);
        Files.Files.TryRemove(row.StoredFileName, out _);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ConversationFor(NoaId).DownloadAttachmentAsync(_ticketId, _openingFile, "10.0.0.1", CancellationToken.None));
        Assert.Empty(AllAuditLogs());
    }

    private int AddAttachmentAt(DateTime at, int? commentId, string fileName)
    {
        var id = AddAttachment(_ticketId, commentId is null ? NoaId : LiatId, commentId, fileName);
        using var db = CreateContext(signedInUserId: null);
        db.TicketAttachments.Find(id)!.CreatedAt = at;
        db.SaveChanges();
        return id;
    }
}

using Common.DTOs.Tickets;
using Common.Enums;
using Repositories.Data;
using Repositories.Interfaces;
using Services.Tests.Fakes;

namespace Services.Tests.Tickets;

/// <summary>
/// New tickets with files (spec 4.3, 9.2), and the cleanup when the database save fails: no file stays on disk without
/// its row.
/// </summary>
public sealed class AttachmentRollbackTests : TicketTestBase
{
    private const string Description = "לא קיבלתי את תלוש השכר של החודש";

    [Fact]
    public async Task A_new_ticket_keeps_its_files_without_a_comment()
    {
        var created = await ServiceFor(NoaId).CreateAsync(new CreateTicketRequest { Description = Description },
            [TestFiles.PdfFile("../../תלוש.pdf"), TestFiles.File("מסך.jpg", TestFiles.Jpeg)], CancellationToken.None);

        var attachments = AllAttachments().Where(a => a.TicketId == created.Id).OrderBy(a => a.Id).ToList();
        Assert.Equal(["תלוש.pdf", "מסך.jpg"], attachments.Select(a => a.OriginalFileName));
        Assert.All(attachments, a =>
        {
            Assert.Null(a.CommentId);
            Assert.Equal(NoaId, a.UploadedByUserId);
            Assert.Equal(Now, a.CreatedAt);
            Assert.True(Files.Files.ContainsKey(a.StoredFileName));
        });
        Assert.Equal(["application/pdf", "image/jpeg"], attachments.Select(a => a.ContentType));
    }

    [Fact]
    public async Task Invalid_files_reject_the_whole_ticket()
    {
        await Assert.ThrowsAsync<Common.Exceptions.ValidationException>(() => ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Description = Description },
            [TestFiles.PdfFile(), TestFiles.PdfFile(), TestFiles.PdfFile(), TestFiles.PdfFile()], CancellationToken.None));

        Assert.DoesNotContain(AllTickets(), t => t.RequesterId == NoaId);
        Assert.Empty(Files.Files);
    }

    [Fact]
    public async Task When_the_database_save_of_a_new_ticket_fails_the_written_files_are_deleted()
    {
        UnitOfWorkFactory = _ => new FailingUnitOfWork();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Description = Description },
            [TestFiles.PdfFile(), TestFiles.File("b.png", TestFiles.Png)], CancellationToken.None));

        Assert.Equal(2, Files.Deleted.Count);
        Assert.Empty(Files.Files);
    }

    [Fact]
    public async Task When_the_database_save_of_a_comment_fails_the_written_files_are_deleted()
    {
        var id = AddTicket(NoaId, TicketStatus.InProgress, Now.AddDays(-1), configure: t => t.AssignedToUserId = LiatId);
        UnitOfWorkFactory = _ => new FailingUnitOfWork();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ConversationFor(NoaId).AddCommentAsync(id,
            new CreateCommentRequest { Body = "תגובה" }, [TestFiles.PdfFile()], CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ConversationFor(NoaId).AddAttachmentsAsync(id, [TestFiles.PdfFile()], CancellationToken.None));

        Assert.Equal(2, Files.Deleted.Count);
        Assert.Empty(Files.Files);
    }

    [Fact]
    public async Task A_cancelled_request_deletes_the_files_too()
    {
        UnitOfWorkFactory = _ => new FailingUnitOfWork(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Description = Description }, [TestFiles.PdfFile()], CancellationToken.None));

        Assert.Empty(Files.Files);
    }

    [Fact]
    public async Task After_a_successful_save_nothing_is_deleted()
    {
        await ServiceFor(NoaId).CreateAsync(new CreateTicketRequest { Description = Description }, [TestFiles.PdfFile()],
            CancellationToken.None);

        Assert.Empty(Files.Deleted);
        Assert.Single(Files.Files);
    }

    /// <summary>The database is down at the moment of the save.</summary>
    private sealed class FailingUnitOfWork(Exception? exception = null) : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw exception ?? new InvalidOperationException("Database unavailable.");

        public void DiscardChanges() { }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

using Common.Enums;
using Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Services.Tests.Tickets;

/// <summary>TicketAccess over the database: the viewer and the ticket are read from it, never from the token.</summary>
public sealed class TicketAccessTests : TicketTestBase
{
    [Fact]
    public async Task The_department_manager_scope_comes_from_the_departments_table()
    {
        var noas = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-2), configure: t => t.AssignedDepartmentId = HrId);

        var asRon = await AccessFor(RonId).GetAsync(noas, CancellationToken.None);
        Assert.True(asRon.Permissions is { CanView: true, CanSeeInternal: true, CanReply: false });

        // Ron is replaced as the manager of Development: the same token now gives no scope.
        await using (var db = CreateContext(RonId))
        {
            (await db.Departments.SingleAsync(d => d.Id == DevId)).ManagerUserId = IdoId;
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<NotFoundException>(() => AccessFor(RonId).GetAsync(noas, CancellationToken.None));
    }

    [Fact]
    public async Task The_handlers_queue_is_their_current_department()
    {
        var id = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-2), configure: t => t.AssignedDepartmentId = HrId);
        Assert.True((await AccessFor(LiatId).GetAsync(id, CancellationToken.None)).Permissions.CanView);

        await using (var db = CreateContext(LiatId))
        {
            (await db.Users.SingleAsync(u => u.Id == LiatId)).DepartmentId = DevId;
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<NotFoundException>(() => AccessFor(LiatId).GetAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task An_inactive_user_with_a_still_valid_token_is_signed_out()
    {
        var id = AddTicket(OritId, TicketStatus.New, Now.AddHours(-2));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => AccessFor(OritId).GetAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Another_organizations_ticket_is_not_found_even_for_the_executive()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => AccessFor(AvnerId).GetAsync(OrgBTicketId, CancellationToken.None));
    }
}

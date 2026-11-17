using System.Data.Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Repositories.Data.Interceptors;

/// <summary>
/// Bumps the organization's <see cref="ITicketDataVersion"/> after any of its tickets was saved (spec 11.3), so the
/// cached dashboards are not used any more. In one place on purpose: ticket creation, status changes, classification,
/// assignment, bulk updates, the AI worker and the automatic closing all save a changed <see cref="Ticket"/> through
/// SaveChanges – a new feature that changes tickets cannot forget to invalidate the dashboards.
/// <para>
/// Timing: without an explicit transaction – after SaveChanges succeeded; inside one (IUnitOfWork.ExecuteInTransactionAsync)
/// – only after the commit, so a dashboard read between the save and the commit cannot store the old numbers under the
/// new version. A save that failed outside a transaction bumps nothing. An extra bump is harmless (one recomputation);
/// a missing one would show stale numbers for up to a minute.
/// </para>
/// <para>
/// ExecuteUpdate/ExecuteDelete do not go through SaveChanges: a repository that changes tickets in bulk calls
/// <see cref="AppDbContext.NoteTicketsChangedOutsideTracking"/> (handler deactivation, TicketRepository).
/// Comments, attachments and internal notes change no metric; a visible comment updates Ticket.UpdatedAt anyway.
/// </para>
/// </summary>
public sealed class TicketChangesInterceptor(ITicketDataVersion versions) : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushUnlessInTransaction(eventData.Context);
        return result;
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        FlushUnlessInTransaction(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData) => DiscardUnlessInTransaction(eventData.Context);

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        DiscardUnlessInTransaction(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        Flush(eventData.Context);

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        Pending(eventData.Context)?.Clear();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Pending(eventData.Context)?.Clear();
        return Task.CompletedTask;
    }

    private static void Collect(DbContext? context)
    {
        if (Pending(context) is not { } pending)
        {
            return;
        }

        foreach (var entry in context!.ChangeTracker.Entries<Ticket>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                pending.Add(entry.Entity.OrganizationId);
            }
        }
    }

    private void FlushUnlessInTransaction(DbContext? context)
    {
        if (context?.Database.CurrentTransaction is null)
        {
            Flush(context);
        }
    }

    private static void DiscardUnlessInTransaction(DbContext? context)
    {
        if (context?.Database.CurrentTransaction is null)
        {
            Pending(context)?.Clear();
        }
    }

    private void Flush(DbContext? context)
    {
        if (Pending(context) is not { Count: > 0 } pending)
        {
            return;
        }

        foreach (var organizationId in pending)
        {
            versions.Bump(organizationId);
        }

        pending.Clear();
    }

    private static HashSet<int>? Pending(DbContext? context) => (context as AppDbContext)?.PendingTicketVersionBumps;
}
